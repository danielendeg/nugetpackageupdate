
# Log Analytics Schema definittion

## Application Audit log

|Field Name|Type|Notes|JSON path in Shoebox|
|---|---|---|---|
|TimeGenerated|DateTime|Required in all schemas|$.time|
|OperationName|String|Required in all schemas|$.operationName|
|CorrelationId|String|Required in all schemas|$.correlationId|
|RequestUri|String||$.properties.requestUri|
|OperationName|String| |$.properties.operationName|
|OperationResult|String| | |
|ResultType|String||$.properties.resultType|
|AuditEventCategory|String||$.properties.auditEventCategory|
|CallerIPAddress|String|P2|$.properties.callerIPAddress|
|CallerIdentityType|String||$.properties.callerIdentityType|
|CallerIdentity|String||$.properties.callerIdentity|


