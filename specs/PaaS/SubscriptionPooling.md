[[_TOC_]]
# Background
In the current design of managed service, all the infrastructure pieces as well as customer resources (IPs, Load Balancer, CosmosDB etc.) are allocated in single subscription (Resolute Prod). This approach has downside of running into limits (known and unknown) at some point as our service continues to grow. Recently, our subscription ran into limits for number of resource groups. This is a hard limit imposed by Azure and harder to raise. To alleviate this problem and continue to grow our services, it is imperative to spread our resources across different subscriptions. 

# Types of subscription
Moving forward, our production subscriptions will be divided in following two types of subscription. 

## System subscription
System subscription is the sub where all the infrastructure pieces of the managed service are provisioned. This includes but not limited to service fabric clusters, Global Keyvaults etc. 

## Account Resource subscription
Account Resource subscription holds all resources provisioned for customer account. It includes but not limited to customer specific IPs, Cosmos DBs, Application Insights etc. Resources in this subscription will be deprovisioned when a customer deprovisions their account. Naming convention of the subscription type is going to be "Resolute Prod Account Resource-XX"  where XX represents a number e.g. "Resolute Prod Account Resource-1", "Resolute Prod Account Resource-2" etc. 

# Adding new Account Resource subscriptions
Right now, we are adding one subscription as a measure to alleviate pressure on Resolute Prod subscription. As a part of this process, we will add documentation for adding new Account Resource Subscription into the mix. There is no plan to remove a subscription from the pool once it is added. As accounts in that subscription are deprovisioned, RP worker will have to have a way to authenticate against that subscription and remove the resources. New account resource subscription shall follow same PIM policies and guidelines in effect at time of addition, it should be managed by <code>Azure Management group</code>. Each additional account resource subscription should be added into <code>EnvrionmentGroup</code> file. Current property <code>subscriptionId</code> in the file will be renamed to <code>systemSubscriptionId</code> and there will be an additional propety named <code>accountResourceSubscriptionIdsRawString</code> will be added. This property will contain semicolon delimited list of account resource subscriptions. 

# Assigning account resources to a account resource subscription
At the beginning, when we have only one account resource subscription, it is simple to assing all the subsequent account resources to that subscription. However, as we continue to add more subscription to the pool, it will require more sophisticated mechanism to assign account resource to a subscription. For now, it makes sense to track resource limits like number of resource groups in a subscription and balance out subscriptions based on that metric. In future we may discover more resource limitations that can not be raised, and these should be taken into consideration when assigning account resources to a subscription. This logic should reside in <code>SubscriptionProvider.cs</code> 

# Transition from single subscription to multi subscription.

Our current production subscription <code>Resolute PROD (680180cd-e78d-4fd0-8fd3-6008bab2b07d)</code> will transition to system subscription and we will stop provisioning customer resources in that subscription. Now, there are no plans to support multiple system subscriptions. 

Currently there are about 900 accounts in existing prod subscription. As of this writing, there is no plan to move these accounts from the existing prod subscription. Primarily because we don't know how the moving of the subscription will affect availability of the services for our customers. 

Now, plan is to add only one subscription as Account Resource subscription to alleviate pressure on the <code>System Subscription</code>. However long term, we will add more subscription before we reach limits on the existing set of subscription. We will create a few account resource subscriptions and add them to the pool.

Right now, we are not cascading subscription deprovisioning/deletion to our resources. This issue exacerbates resource group limit problem. In near future, subscription state change should be handled properly, this will continue to drain accounts out of system subscription. Even at this rate, it is inevitable that we will hit resource group limits in system subscription. At some point in future, we shall transition current customer accounts from <code> Resolute Prod</code> subscription to account resource subscription. However, this effort is not in scope for current transition. 

For test/rel environment there we will add additional subscription to emulate production like configuration. 

# Monitoring of subscriptions' resource limits
All the subscriptions should be monitored for their respective resource limits. Currently we only monitor number of CosmosDB for System subscription. We shall have document per subscription in our global database to track these limits. <code>AzureResourceMonitor.cs</code> shall be extended to monitor each of this subscription. As these subsciptions start to approach limits, subsequent ICM alerts should contain subscription id for which the alerts are being raised. 

# Scope of changes
## PowerBI reporting changes
### Update Publish-AzureQuotaUsage.ps1 for multiple subscription.
## Powershell script changes
## Service environment
## Schema changes
### Account entity/resource entity
Add backfill logic into the code or run one time data migration via Resolute Multi tool.
Edit: After discussions, Shawn and I decided to go the Resolute Multi Tool path. Shawn will add a command there. 
## Resource provider worker
## Billing agent
## Key Rotator
### Global key rotator (test only, may not need actual changes)
### Account key rotator
## Azure Resource Monitor
## Additional tooling to figure out in which subscription the account resources are located. 
### TBD