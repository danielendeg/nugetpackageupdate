ARM requires us to handle state changes of subscription as outlined [here](https://github.com/Azure/azure-resource-manager-rpc/blob/master/v1.0/subscription-lifecycle-api-reference.md). Azure API for FHIR is currently does not handle all the states correctly. This work outlines effort to meet the requirements outlined in the ARM spec.  

[[_TOC_]]

# Business Justification

Currently Azure API for FHIR doesn't handle Warned, Suspended, Deleted correctly. As a result of this, often we have resources like <code>CosmosDB</code> left behind when a customer subscription is deleted. This results in us continuing to pay for the resources for which we can not bill. This also contributed to Sev2 incident back in April 2020 where we hit resource group limits. Had we been cascading deletes as outlined in the document, that would have potentially given us little bit more room. Right now, cleaning up these orphaned resources is a manual process which DRIs must do frequently. Being compliant with this requirement will alleviate need for this manual process. 

# Scenarios

## Subscription State Transition

![External - Subscription State Transition](./media/SubscriptionStateTransition.png)

Subscription State | Description
------- | ---------------- 
Registered  |   The subscription was entitled to use your "ResourceProviderNamespace". Azure will use this subscription in future communications. You may also do any initial state setup because of this notification type. When a subscription is "fixed" / restored from being suspended, it will return to the "Registered" state. All management APIs must function (PUT/PATCH/DELETE/POST/GET), all resources must run normally, Bill normally.  
Warned | The subscription has been warned (generally due to forthcoming suspension resulting from fraud or non-payment). Resources must be offline but in running (or quickly recoverable state). Do not deallocate resources. GET/DELETE management APIs must function; PUT/PATCH/POST must not. Don't emit any usage. Any emitted usage will be ignored.        
Suspended  | The subscription has been suspended (generally due to fraud or non-payment) and the Resource Provider should stop the subscription from generating any additional usage. Pay-for-use resource should have access rights revoked when the subscription is disabled. In such cases the Resource Provider should also mark the Resource State as "Suspended." We recommend that you treat this as a soft-delete so as to get appropriate customer attention. GET/DELETE management APIs must function; PUT/PATCH/POST must not. Don't emit any usage. Any emitted usage will be ignored.        
Deleted   | The customer has cancelled their Windows Azure subscription and its content *must* be cleaned up by the resource provider.The resource provider does *not* receive a DELETE call for each resource – this is expected to be a "cascade" deletion.
Unregistered| Either the customer has not yet chosen to use the resource provider, or the customer has decided to stop using the Resource Provider. Only GETs are permitted. In the case of formerly registered subscriptions, all existing resources would already have been deleted by the customer explicitly.

# Metrics

Following metrics shall be tracked as part of this work. 

1. Subscription and its target state
1. Time to execute the action by subscription state (registered, unregistered, warned, suspend or delete)
1. Emit metrics for failed subscription state change operation(s).

# Design
In general, when our service receives a notification in subscription state change, these states should be cascaded down to corresponding Azure API for FHR accounts. This will ensure that we are not having to make two jumps to global db get to the state of the account. This way, account subscription status is readily available on the account object itself. To accomplish this, we will add <code>State</code> property on the account. 

As described earlier, there are a few bulding blocks that can accomplish responding to all 3 state implementation. We will talk about design of each of those actions by suscription state in detail in this section. 

## Building blocks.
Reading through all the requirements following are the building blocks of implementing this feature. 

### Taking resources offline.
As part of suspended and warned state, customer should not be able to use the Azure API for FHIR. Taking a resource offline involves two step process. 
1. Marking all the Azure API for FHIR accounts for that subscription suspended/warned.
1. Blocking the traffic at the frontend for account(s) with suspended/warned subscription state. Account Routing services refreshes it's cache every 5 mins, we will let customer access the service until then. We will also allow health/check traffic to go through for the account(s) with suspended/warned subscription state.

#### Marking an account suspended/warned. 
When an account is marked as suspended, frontend server shall not forward traffic to backend nodes. This will ensure if someone has cached the IP address of our frontend servers then they are not able to use the services. 

This can be achieved by updating <code>GetRoutingInfoByDomain</code> and <code>ListRoutingInfoAsync</code> to check for account subscription status and filtering out accounts with suspended and warned status. This would mean that there will be some delay between the subscription is marked as suspended/warned to the time front end stops routing requests to backend. These suspended/warned accounts won't show up in the list of accounts. We can also bubble this up to the frontend service and return <code> 403 Forbidden </code> status code.

#### Re-Instate accounts.
As soon as the subscription goes back to "Registered" state from "Suspended/Warned" state, on the control plane side we will queue an internal "ReinstateAccounts" operation type that would mark the subscription state as "Registered/Active" for all the accounts in that subscription. On the data plane side, we will rebuild the index and resume customer's access to the service.

### Stop emitting usage.
This is a requirement from Azure. This can be implemented in <code>BillingAccountGetter.cs</code> by checking <code>AccountSubscriptionState</code> property. This will cause us to incur cost for keeping the CosmosDB around. However, it is same scenario today. 
### Delete resources.
When a subscription is deleted, delete all the accounts under that subscription. If IoMT/IOT or Private Link resources are associated with any of the accounts delete those resources as well. Existing functionality to deprovision the account should be leveraged here. 

### Finding all accounts associated with a subscription.
It is possible that a subscription has multiple Azure API for FHIR accounts associated with it. As part of this work, we will need to write a method <code>FindAccountsBySubscription</code> to find all of the accounts associated with a subscription in <code>AccountRegistryManagementRepository.cs</code> class. This will ensure that the actions on the subscription can be cascaded to underlying accounts. 

### Preventing PATCH/PUT/POST from functioning
We will add this functionality in the parent class of the <code>ServiceTypeHandler.cs</code> class where all public methods except for <code>OnDeleteResourceAsync</code> <code>OnEnumerateOperations</code> shall check for account/subscription state as part of validation before actually executing the command. If subscription is in warned/suspended state, then these methods should return Http status code 403. Any Typehandler that inherits from this parent class will be able to use this functionality.

### Handling subscription state change notifications
ARM can send a "Warned" state subscription notification right before "Registered" state subscription notification. To process these state changes in the same order we received them and persist this data on our end, we will queue an internal "Subscription State Change" operation in the RP worker and process it based on the timestamp in an ascending order. Right now, we can queue only 1 operation. That will change in this approach and we would be able to queue multiple subscription state change operations.
Between subscription state change operation and customer initiated operation, there will be no change in the way how RP worker processes these operations. Whichever comes first will get processed. However, if we get a customer initiated operation when there is a subscription state change operation in progress, we will error out the customer initiated operation and return appropriate error message to the customer.
For both subscription state change and completed subscription state change operations, we will use unique ids.
We will also add a "LastModifiedBy" field in the operation document to track the system vs user updates.

Below is just a snippet of what a class would look like.

```c#
   public class SubscriptionStateChangeOperationDocument : OperationDocument
    {
        public const string DocumentType = "subscriptionstatechangerequest";

        public SubscriptionStateChangeOperationDocument()
        {
            SearchIndex = new SearchIndexDictionary<SubscriptionStateChangeOperationDocument>(
                this,
                p => p.SubscriptionId,
                p => p.ProviderNamespace);
        }

        public SubscriptionState State { get; set; }

        public string LastModifiedBy { get; set; }
    }
```
## Warned state.
In warned state, resources should be taken offline, and only GET/DELETE management APIs shall function. This action will be executed asynchronously.  

This is a three-part action. 
1. Update the subscription metadata itself via subsription repository. 
1. Update all the corresponding accounts to reflect new subscription state. 
1. Take resource offline

Once action is successfully completed, Azure API for FHIR account should show status of the account as "Offline" in Azure portal. This can be accomplished by exposing account subscription status on the contract and have portal code update the status of the account accordingly. 

## Suspended state
This state is same as warned state in addition to treat resources as "soft delete". In our service, since we dont have a soft delete for our service this will be same as warned state. Even though this state is functionally same as warned state, subscription and account document should reflect the account subscription state as suspended and not as warned. 

## Deleted state
When our service receives a state change notification with state deleted, all the underlying Azure API for FHIR account should be deprovisioned including its CosmosDB. Since a typical deletion of CosmosDB takes upwards of 30 minutes, this operation should be executed asyncronously. 

In this action 
1. Find all the accounts associated with the subscription.
1. Delete private link resources for the accounts if they exist. 
    Note that Private Link resources have a dependency on NRP which we usually clear by calling NRP via ARM. Once a subscription is in a deleted state all RPs will receive a subscription lifecycle event with this new state and all operations back into ARM won't work or be forwarded to other RPs. To handle it gracefully, we will clear these resources from account document without calling NRP.
1. Queue deprovision documents for each of the account.

Since this is a destructive operation, roll out of this action should be a two phased approach. 

In the phase 1, 

- Our service should start logging which subscriptions it got delete notification for.
- Identify the accounts that should be deleted.
- Verify that RP is able to handle the above 2 things correctly.
- Identify/fix bugs if any.
- We expect to be in this phase for 2-3 weeks. Once we verify that this scenario is being handled correctly as noted above, we will move onto the phase 2.

In phase 2, this action should enable deletion of the resources, as expected.

## Private link and IoT enabled accounts. 
Private Link and IoT are features of Azure API for FHIR. In both cases we depend on external service to deliver data to Azure API for FHIR. Once we implement these states, Azure API for FHIR will stop accepting any data once the subscription is in warned/suspended state. In this scenario, a cusotmer could still be sending data or trying to get the data however our serivce will neither serve or updat the data. Thus customer's access to data may be cut off almost immediately once we have receird the request. 

In case a Private Link resource exist in customer's susbscription, it is possible that Private Link service stop accepting data from customer before we suspend or warn the account. In case, if Azure API for FHIR marks account as suspended or warned then it is possible that a cusotmer will push their data via private link but we will refuse to accept the data by returning <code>403 forbiddern</code> error. 

In case if IoT is enabled on the account a similar logic would apply. Since IoT accepts data via eventhub, there is no simple way of simply stop accepting data at IoT endpoint. Dustin and I spoke at length about this, agreement now is, Azure API for FHIR will stop accepting data. IoT will stop processing data and hence will stop posting data to FHIR service. However, since we dont control eventhub directly, we are unable to stop accepting data at eventhub level. This may cause messages to build up at eventhub level. This work will be owned by IoT team.

## Gen2

#### Handle Suspended/Warned subscription state
All the child services(DICOM/IoT/FHIR) are responsible for blocking customer's access to the data after receving "Suspended/Warned" subscription state change notification. Data plane changes for the FHIR service are outlined above. Work required for IoT service will be owned by IoT team. Notified the DICOM team about this as well.

### Cascade delete
There are 2 possible options as listed below. I had a sync with Dustin on this.

1. Let the "SubscriptionStateChange" operation clean up both Gen1 and Gen2 resources in that subscription. This is possible because RP worker will have both libraries in scope.
1. Create 2 different operations for cleaning up Gen1 and Gen2 resources. That way in the future if we end up moving RP worker code to AKS, it can be split off.

Based on the feedback, we will proceed with 2nd option i.e create 2 different operations for cleaning up Gen1 and Gen2 resources.

# Test Strategy
Unit tests, end to end tests will be added for the scenarios.

Testing subscription state change events (Suspended, Restored) can be done through the Subscriptions blade in the dogfood portal.

## Suspend subscription state:

1. In the dogfood portal, search for subscriptions in the top search bar (select the subscriptions result)
2. Select the subscription you wish to suspend/re-instate from the list on the subscriptions blade.
3. On the overview page, you will have a "Cancel Subscription" option.
4. Once selected, you will be taken to a warning page, select "Ignore and cancel"
5. provide the subscription name and reason to verify and click "cancel subscription"

## Re-Instate subscription state:
If the subscription is in suspended state, you will be given the option on the overview page to re-instate subscription.

## Delete subscription state:
The workaround to test this scenario is to use a custom geneva action which can trigger PUT subscription call with different subscription states. We can not make PUT subscription call without onboarding to Geneve Action.

# Security
*Describe any special security implications or security testing needed.*

# Other

*Describe any impact to privacy, localization, globalization, deployment, back-compat, SOPs, ISMS, etc.*


