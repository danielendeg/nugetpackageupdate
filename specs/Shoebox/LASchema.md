
# Log Analytics Schema definition

## Application Audit log

|Field Name|Type|Notes|JSON path in Shoebox|
|---|---|---|---|
|TimeGenerated|DateTime|Required in all schemas|$.time|
|OperationName|String|Required in all schemas|$.operationName|
|CorrelationId|String|Required in all schemas|$.correlationId|
|RequestUri|String|The request URI|$.uri|
|FhirResourceType|String|The resource type the operation was executed for|$.properties.fhirResourceType|
|StatusCode|Int|The HTTP status code. (e.g., 200)|$.resultSignature|
|ResultType|String|The available value currently are ‘Started’, ‘Succeeded’, or ‘Failed’|$.resultType|
|OperationDurationMs|Int|The milliseconds it took to complete this request.|$.durationMs|
|LogCategory|String|The log category. We are currently emitting ‘AuditLogs’ for the value.|$.category|
|CallerIPAddress|String|The caller’s IP address|$.callerIpAddress|
|CallerIdentityIssuer|String|Issuer|$.identity.claims.iss|
|CallerIdentityObjectId|String|Object_Id|$.identity.claims.oid|
|CallerIdentity|Dynamic|A generic property bag containing identity information.|$.identity|
|Location|String|The location of the server that processed the request(e.g., South Central US)|$.location|
