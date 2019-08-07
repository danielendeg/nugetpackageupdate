
# Log Analytics Schema definittion

## Application Audit log

|Field Name|Type|Notes|JSON path in Shoebox|
|---|---|---|---|
|TimeGenerated|DateTime|Required in all schemas|$.time|
|ResourceId|String|The Azure resource id.|$.resourceId|
|OperationName|String|Required in all schemas|$.operationName|
|CorrelationId|String|Required in all schemas|$.correlationId|
|RequestUri|String|The request URI|$.uri|
|FhirResourceType|String|The resource type the operation was executed for|$.identify.FhirResourceType|
|ResultSignature|String|The HTTP status code. (e.g., 200)|$.resultSignature|
|ResultType|String|The available value currently are ‘Started’, ‘Succeeded’, or ‘Failed’|$.resultType|
|OperationDuration|String|The milliseconds it took to complete this request.|$.durationMs|
|AuditEventCategory|String|The log category. We are currently emitting ‘Audit’ for the value.|$.category|
|CallerIPAddress|String|The caller’s IP address|$.callerIpAddress|
|IdentityIssuer|String|Issuer|$.identity.iss|
|CallerIdentity|String|Object_Id|$.identity.oid|
|Location|String|The location of the server that processed the request(e.g., South Central US)|$.location|


