# Design for adding customer data to Audit Logs

The goal is to allow customers to add custom information to the audit logs.

[[_TOC_]]

# Scenarios

* Include additional information that occurs at a different layer than the FHIR service, for example Azure API management.  Some examples:
  * Identity or authorization information
  * Origin of the caller
  * Originating organization
* Include additional information directly from the client, such as the calling system (EHR, patient portal)

# Design

Overall this design will build on the already designed/implemented logging mechanisms in OSS and PaaS.  This includes using the Audit middleware, audit helper and logging infrastructure in OSS and also the Shoebox implementation in PaaS to surface these values to the end users.  [Shoebox spec](../Shoebox/DiagnosticLoggingDesignSpec.md)

Any http header named with the following convention: 
```X-MS-AZUREFHIR-AUDIT-<name> ```
will be included in a property bag that is added to the Audit log.  Examples:
```
X-MS-AZUREFHIR-AUDIT-USERID: 1234
X-MS-AZUREFHIR-AUDIT-USERLOCATION: XXXX
X-MS-AZUREFHIR-AUDIT-XYZ: 1234
```

## OSS
We will use the existing Audit middleware to add the headers to the audit log. A set of classes will be added to extract and validate the custom headers from the http context when the audit log is being created.  The IAuditLogger interface will be changed to include a new property bag populated from the custom headers.  This will be passed into the ```LogAudit``` method in ```AuditLogger```. For the OSS implementation that property bag will be combined into a single string very much like the claims information.



``` C#
public void LogAudit(
    AuditAction auditAction,
    string action,
    string resourceType,
    Uri requestUri,
    HttpStatusCode? statusCode,
    string correlationId,
    string callerIpAddress,
    IReadOnlyCollection<KeyValuePair<string, string>> callerClaims,
    IReadOnlyCollection<KeyValuePair<string, string>> customerHeaders)  // new parameter here, update to the interface
{
    string claimsInString = null;
    string customerHeadersInString = null;

    if (callerClaims != null)
    {
        claimsInString = string.Join(";", callerClaims.Select(claim => $"{claim.Key}={claim.Value}"));
    }

    if (customerHeaders != null)
    {
        customerHeadersInString = string.Join(";", customerHeaders.Select(header => $"{header.Key}={header.Value}"));
    }

    _logger.LogInformation(
        AuditMessageFormat,
        auditAction,
        AuditEventType,
        _securityConfiguration.Authentication?.Audience,
        _securityConfiguration.Authentication?.Authority,
        resourceType,
        requestUri,
        action,
        statusCode,
        correlationId,
        callerIpAddress,
        claimsInString,
        customerHeadersInString);  // new property here
}
```

## PaaS
The ```FhirOperationAuditLogger``` class will be updated similar to the ```AuditLogger``` class.  It will add the name/value pairs from the custom headers to the existing property bag which is currently being passed to the ```LogAudit``` call for ```AzureMonitorAuditLogger```.  This will then finally be surfaced in the properties bag column in Shoebox.  When calling the IfxAuditLogger class, the custom header properties will not be added, as we don't want customer data in the Ifx logs.

### Property bag in Shoebox schema
Note: This log schema is the Azure diagnostic logs schema. https://docs.microsoft.com/en-us/azure/azure-monitor/platform/diagnostic-logs-schema 
| Name           | Datatype            | Description                                                                  
|----------------|---------------------|------------|
| properties     | string              | Any extended properties related to this particular category of events. All custom/unique properties must be put inside this “Part B” of the schema.

``` C#
public void LogAudit(
FhirAuditAction auditAction,
string action,
string resourceType,
Uri requestUri,
HttpStatusCode? statusCode,
string correlationId,
IReadOnlyCollection<KeyValuePair<string, string>> claims,
IReadOnlyCollection<KeyValuePair<string, string>> customHeaders)
{
IReadOnlyCollection<IIdentity> callerIdentities = claims?.Select(c => new ClaimIdentity(c.Key, c.Value)).ToList();

ICollection<AuditProperty> properties = CreateAuditProperties().ToArray();

foreach (ILogAudit auditLogger in _auditLoggers)
{
    if (!(auditLogger is AzureMonitorAuditLogger) || _canAuditFhirOperationToAzureMonitor)
    {
        if (auditLogger is AzureMonitorAuditLogger)
        {
            properties.AddRange(ConvertToAuditProperties(customHeaders));
        }
        try
        {
            auditLogger.LogAudit(
                auditAction == FhirAuditAction.Executed ? AuditAction.Executed : AuditAction.Executing,
                _fhirServiceEnvironment.FhirServiceResourceId,
                $"{_fhirServiceEnvironment.FhirServiceOperationNamePrefix}{action}",
                requestUri,
                statusCode,
                correlationId,
                null,
                _fhirServiceEnvironment.ClusterRegion,
                callerIdentities,
                properties);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to write audit log using {AuditLoggerType}.", auditLogger.GetType().Name);
        }
    }
}
```

## Limits on Custom headers
We will limit the number of custom headers to 10 and the size of each header value to 2k.
That would be a total allowable custom header size of 20 kb.  As a [reference](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.server.kestrel.core.kestrelserverlimits.maxrequestheaderstotalsize?view=aspnetcore-2.2#Microsoft_AspNetCore_Server_Kestrel_Core_KestrelServerLimits_MaxRequestHeadersTotalSize), Kestrel limits the total size of all headers to 32 kb.

In the event that the headers fail validation, either on count or size, we will throw an exception and return an http 431 code with a FHIR operation outcome failure.

# Test Strategy

* We can test basic inclusion of the headers in the Audit call using unit tests.
* E2E tests will be needed to check for the accessibility of the values in either App Insights for OSS or Shoebox for PaaS.
* Fuzz testing will be done to check for different types of the values which may cause problems
* Limit testing for the size and number of headers will be done.

# Security

Three main points came out of the security review:
1) Clear guidance for the end users about how to use the headers and what kind of information should be placed there.  In particular, if any sensitive information is added in these headers that it should be encrypted prior to being added to the http headers.
2) We should lock down the tables in geneva/shoebox which are used to store the audit logs so that it has limited access.
3) We will need a privacy review and to update the Data Inventory and Retention Policies

# Other

