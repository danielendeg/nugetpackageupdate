This document outlines how the the ResourceProviderWorker initiates a request to the FhirApplication to update properties without the need to re-provision or restart.

This document includes design details to relating the ResourceProviderWorker only. Design details for the AccountRoutingService can be found [here](AccountRoutingService.md) and details regarding the FhirApplication can be found [here](FhirApplication.md).

[[_TOC_]]

# Business Justification

As outlined in the [Updating-Resource-Properties](../Updating-Resource-Properties.md) document, updating properties like Authentication/Authorization settings and CORS requires re-provisioning every Azure resource that backs the API for FHIR. The ability to update some properties without re-provisioning resources will greatly improve the user experience.

# Scenarios

Supported Scenarios listed below have links to individual docs that outline specific business justifications, test strategies, etc.

1. As an "Azure Api for Fhir" service user, I want to provision a new FHIR Server.
2. [As an "Azure Api for Fhir" service user, I want to add an IoMT Connector.](IoMTConnectorSettings.md)
3. As an "Azure Api for Fhir" service user, I want to add or remove an oid from the list of allowed Oids.
4. As an "Azure Api for Fhir" service user, I want to change the authentication Authority.
5. As an "Azure Api for Fhir" service user, I want to change the authentication Audience.
6. As an "Azure Api for Fhir" service user, I want to update the CORS configuration.

# Metrics

* Requests made to update FHIR Server settings, the duration of the request and result.

# Design

The IFhirServerSettingsUpdater will provide a way for Commands to update FHIR Server settings after the service has been deployed, without requiring a restart.

1. The OperationDocument is passed into the UpdateServerSettingsMethod.
2. A POST request is made to the FHIR Server settings endpoint with the operation document id.
3. The method will throw if there is an error, or the task will complete.

**Note: The settings request is idempotent so a FhirServerSettings object includes ALL settings, not just settings that have been updated or added.**

```c#
public interface IFhirServerSettingsUpdater
{
    /// <summary>
    /// Handles requests to update FHIR Server settings.
    /// </summary>
    /// <param name="operation">The operation currently executing.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task UpdateFhirServerSettings(OperationDocument operation, CancellationToken cancellationToken);
}
```

# Test Strategy

Unit tests:

* FhirServerSettingsUpdater

# Security

The key security principle that we adhere to here is ensuring that each service has access to no more information than it requires, and should never be able to access settings or data belonging to another service. A 'fhirServerSettings' role will be added to the FhirApplication and the role will only be authorized to access to the settings endpoint.
