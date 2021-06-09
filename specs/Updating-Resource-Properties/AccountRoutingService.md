This document outlines how the AccountRoutingService will provide the FhirServerSettings objects to the FHIR Server.

This document includes design details relating the AccountRoutingService only. Design details for the FhirApplication can be found [here](FhirApplication.md) and details regarding the ResourceProviderWorker can be found [here](ResourceProviderWorker.md).

[[_TOC_]]

# Business Justification

As outlined in the [Updating-Resource-Properties](../Updating-Resource-Properties.md) document, updating properties like Authentication/Authorization settings and CORS requires re-provisioning every Azure resource that backs the API for FHIR. The ability to update some properties without re-provisioning resources will greatly improve the user experience.

# Metrics

We will track:

1. Requests made from the FhirApplication to the settings endpoint, the duration and result of the requests.

# Design

A FhirServerSettingsController will be added to the AccountRoutingService that will expose an endpoint for requests to GET FhirServerSettings. The request will be delegated to IFhirServerSettingsProvider, which will:

1. Retrieve the OperationDocument and Account document from the Global DB.
2. Create a FhirServerSettings object using properties contained in the two documents.
3. Return the FhirServerSettings or an error to the FhirApplication.

```c#
public interface IFhirServerSettingsProvider
{
    /// <summary>
    /// Handles requests to update FHIR Server settings.
    /// </summary>
    /// <param name="operationDocumentId">The id of an operation document in the Global db.</param>
    /// <param name="subscriptionId">The subscription id (used as a partition key) for the settings document.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A FhirServerSettings object.</returns>
    Task<FhirServerSettings> GetFhirServerSettings(string operationDocumentId, CancellationToken cancellationToken);
}
```

# Test Strategy

Unit testing:

* FhirServerSettingsProvider

# Security

The key security principle that we adhere to here is ensuring that each service has access to no more information than it requires, and should never be able to access settings or data belonging to another service. The request from the FhirApplication will use the same token used to refresh the account specific Cosmos DB resource tokens. The token will be evaluated to ensure that FHIR Servers can only read OperationDocuments and Account documents associated with that particular FHIR Server.
