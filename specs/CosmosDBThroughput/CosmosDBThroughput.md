# Allow to change cosmosdb throughput to more than 10,000 RU/s

# BackGround
Currently, customers are allowed to configure cosmosDB throughput only upto 10,000RU/s, due to the fact that cosmosDB may take longer to complete the provisioning request. This can lead to a weird state where if a client want to change thr RU/s again, it might lead to an error. This design review is to support updating throughput to more than 10,000RU/s. If cosmosDB is taking longer to update the requested  RU’s, azure portal should display the status saying the cosmosdb throughput is updating and not allow to change the throughput if provisioning is in progress already.

## Scenarios
* As a Azure user I want to set a custom RU any value between 400 to 100,000  at service creation time. Default that we show to the customer is 400.
* As a Azure user I want to change RU value to higher or lower within a range of 400 to 100000, depending on current state of the database and RU.
* As a Azure user I will see the updated throughput in the overview page minutes after the request although throughput provisioning is still in progress in the background.
* As a Azure user I will see the status of cosmosDB provisioning if provisioning is still in progress in the background.
* As a Azure user I cannot set the cosmosdb throughput when throughput is still in progress for existing accounts.

# Design
* Based on our observation and testing, CosmosDB allows to request only upto 100,000 RU/s via portal. To request Ru/s more than that, we need to go through customer support. To mirror this behavior on our side, the maximum allowed throughput will be updated to 100,000RU/s.
* Will add a new property in CosmosDB configuration indicating the current maximum allowed throughput.
* CosmosDB provides SDK to update the offer throughput but it doesnot expose the flag "isOfferReplacePending" that indicates the provisioning status.
* Will access the same using dynamic content as follows : offerV2.GetPropertyValue<dynamic>("content").isOfferReplacePending

##Approach
 * Set the maximum upper limit of offerthroughput to 100000 RU/s.
 * Under AccountSpecificCosmosDbPropertyReader, refactor the existing method GetAccountSpecificCosmosDbOfferThroughput, which uses .Net SDK to access the desired property, such that both offerthroughput and isOfferRequestPending can be extracted.
 * Will add following two new properties in CosmosDBConfiguration.

 ```
        [JsonProperty("isOfferReplacePending")]
        public bool IsOfferReplacePending { get; set; } = false;

		 [JsonProperty("maxAllowedOfferThroughput")]
        public int MaxAllowedOfferThroughput { get; set; } = ResourceProviderConstants.MaxOfferThroughput;

```

* In Servicedataprovider, in addition to a updating resourceEntity with actualthroughput, new property "isOfferReplacePending" will also be updated.
* IsOfferReplacePending will also be used during the validation of cosmosdb throughput. Before trigerring the actual provisioning, ServiceHandler will check this property to make sure provisioning is not in-progress for existing accounts. This will be the new method that will be added under CheckCosmosDbOfferThroughput class. 
    
```
	public async Task ValidateIfThroughputChangeIsAllowed(ServiceResourceEntity serviceResourceEntity)
        {
            if (!serviceResourceEntity.IsNew)
            {
                // As cosmosDb can increase actual throughput, which can be greater than max value, depending upon the volume of data for exisiting entities,
                // this validation checks if the requested throughput is greater than max value then actual throughput must also be greater than max value.

                bool isOfferReplacePending = await _accountSpecificCosmosDbPropertyReader.GetAccountSpecificCosmosDbOfferThroughputProvisioningStatuss(
                                                                                            serviceResourceEntity.Id.SubscriptionId,
                                                                                            serviceResourceEntity.InternalFhirAccountProperties.Fhir,
                                                                                            _cancellationTokenResolver.GetCancellationToken()) ?? false;

                if (isOfferReplacePending)
                {
                    throw new ResourceProviderException(
                        string.Format(CultureInfo.InvariantCulture, Resources.ServiceOfferThroughputChangeNotAllowed),
                        HttpStatusCode.BadRequest.ToString(),
                        HttpStatusCode.BadRequest);
                }
            }
        }
```

Validation should fail if isOfferReplacePending is true.

* On portal, the added properties will be accessed via resourceentity.
* On portal, MaxAllowedOfferThroughput will be used as a validation rule for maximum allowed throughput. A message will be displyed indicating that a customer can contant css if they want higher throughput. 
* On portal, IsOfferReplacePending will be used to display cosmosdbThroughput provisioning status if IsOfferReplacePending == true.
* On portal, IsOfferReplacePending will also be used as client side validation to prevent the user from changing the throughput if IsOfferReplacePending == true.

#Testing:
* Test to validate that validation passes for the throughput value in a range between 400 to 100000 inclusive and in multiples of hundreds.
* Test to validate ServiceDataProvider returns a resourceentity with the the newly added cosmosDbConfiguration property.
* Test to validate that the newly added property is handled properly for existing accounts
* Test to validate that RP rejects cosmosDB throughput update if cosmosdb provisioning is still in progress.
* Test to validate that desired property can be extracted from the offerV2 response object

