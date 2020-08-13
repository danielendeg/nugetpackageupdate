## Service Release Details

- Is this an Embargoed Preview, A Public Preview, or a General Release?

    - General Release

- What is the expected service release date?

    - September 2019

## Contact Information

- Main developer contacts (emails + github aliases)

    - poadhika@microsoft.com, poadhika 
	- yazanal@microsoft.com, yazanmsft

- PM contact (email + github alias) 

    - mihansen@microsoft.com, hansenms

- Other people who should attend a design review (email)

    - joycech@microsoft.com

## High Level Scenarios

###Describe how your feature is intended to be used by customers.
	Customers can use the powershell cmdlets to Create/Update/Delete a healthcareApis fhir service within a subscription or a resourcegroup.
	Customers can use the powershell cmdlets get a healthcareApis fhir service or list of services within a subscription or a resourcegroup


###Piping scenarios / how these cmdlets are used with existing cmdlets
	Piping by value using an -InputObject and using a -ResourceId is supported

###Sample of end-to-end usage

1.	Create a service with default configuration
```powershell
New-AzHealthcareApisFhirService -Name "res1234" -ResourceGroupName "rg1234" -Location "westus2"
```

2.	Create a service with custom configuration
```powershell
New-AzHealthcareApisFhirService -Name "res1234" -ResourceGroupName "rg1234" -Location "westus2" -CosmosOfferThroughput 2000
```

3.	Update a service with custom configuration
```powershell
Set-AzHealthcareApisFhirService -Name "res1234" -ResourceGroupName "rg1234" -CosmosOfferThroughput 400
```

4.	Create/Remove a service with default configuration
```powershell
New-AzHealthcareApisFhirService -Name "res1234" -ResourceGroupName "rg1234"
Remove-AzHealthcareApisFhirService -Name "res1234" -ResourceGroupName "rg1234"
```
5.	Remove all healthcareApis services in the current subscription
```powershell
Get-AzResource -ResourceType "Microsoft.HealthcareApis/services" | Remove-AzHealthcareApisFhirService
``````

## Syntax changes
This should include PowerShell-help style syntax descriptions of all new and changed cmdlets, similar to the syntax portion of PowerShell help (or markdown help), for example:

### New Cmdlet
####New-AzHealthcareApisFhirService
##### SYNOPSIS
Creates a new healthcareApis fhir service and save it within the specified subscription and resource group. This returns PSHealthcareApisFhirService
#####SYNTAX
```powershell
New-AzHealthcareApisFhirService 
	-Name <String> 
	-ResourceGroupName <String> 
	-Location <String>
	[-CosmosOfferThroughput <Integer>]
	[-Authority <String>] 
	[-Audience <String>] 
	[-SmartProxyEnabled <Boolean>]
	[-CorsOrigins <String []>] 
	[-CorsHeaders <String[]>] 
	[-CorsMethods <String[]>] 
	[-CorsMaxAge <Integer>] 
	[-CorsAllowCredentials <Boolean>]
	[-AccessPolicyObjectIds <String[]>]
	[-Tags <String[]>] 
	[-FhirVersion <String>] 
	[-SubscriptionId <String>] 
	[-AsJob] 
	[-WhatIf] 
	[-Confirm] 
	[<CommonParameters>]
```

```powershell
New-AzHealthcareApisFhirService 
	-Name <String> 
	-ResourceGroupName <String> 
	-Location <String>
	[-HealthcareApisConfig <PSHealthcareApisFhirServiceConfig>]
	[-Tags <String[]>] 
	[-FhirVersion <String>]
	[-SubscriptionId <String>] 
	[-AsJob] 
	[-WhatIf] 
	[-Confirm] 
	[<CommonParameters>]
```

####Remove-AzHealthcareApisFhirService
##### SYNOPSIS
Deletes an existing healthcareApis fhir service created within the specified subscription and resource group.
#####SYNTAX
```powershell
Remove-AzHealthcareApisFhirService 
	-Name <String> 
	[-ResourceGroupName <String>] 
	[-SubscriptionId <String>] 
	[-AsJob] 
	[-WhatIf] 
	[-Confirm]
	[<CommonParameters>] 
```

#####SYNTAX
```powershell
Remove-AzHealthcareApisFhirService 
	-ResourceId <String>
	[-SubscriptionId <String>] 
	[-AsJob] 
	[-WhatIf] 
	[-Confirm]
	[<CommonParameters>] 
```

#####SYNTAX
```powershell
Remove-AzHealthcareApisFhirService 
	-InputObject <PSHealthcareApisAccount>
	[-SubscriptionId <String>] 
	[-AsJob] 
	[-WhatIf] 
	[-Confirm]
	[<CommonParameters>] 
```

####Set-AzHealthcareApisFhirService
#####SYNOPSIS
Updates an existing healthcareApis fhir service created within the specified subscription and resource group.
#####SYNTAX
```powershell
Set-AzHealthcareApisAccountFhirService
	-Name <String> 
	[-ResourceGroupName <String>] 
	[-CosmosOfferThroughput <Integer>]
	[-Authority <String>] 
	[-Audience <String>] 
	[-SmartProxyEnabled <Boolean>]
	[-CorsOrigins <String []>] 
	[-CorsHeaders <String[]>] 
	[-CorsMethods <String[]>] 
	[-CorsMaxAge <Integer>] 
	[-CorsAllowCredentials <Boolean>]
	[-AccessPolicyObjectIds <String[]>]
	[-SubscriptionId <String>]
	[-Tags <String[]>] 
	[-AsJob] 
	[-WhatIf] 
	[-Confirm] 
	[<CommonParameters>]
```
```powershell
Set-AzHealthcareApisFhirService
	-Name <String> 
	[-ResourceGroupName <String>]
	-HealthcareApisFhirServiceConfig <PSHealthcareApisFhirServiceConfig>
	[-SubscriptionId <String>]
	[-Tags <String[]>] 
	[-AsJob] 
	[-WhatIf] 
	[-Confirm] 
	[<CommonParameters>]
```
```powershell
Set-AzHealthcareApisFhirService
	-ResourceId <String>
	[-CosmosOfferThroughput <Integer>]
	[-Authority <String>] 
	[-Audience <String>] 
	[-SmartProxyEnabled <Boolean>]
	[-CorsOrigins <String []>] 
	[-CorsHeaders <String[]>] 
	[-CorsMethods <String[]>] 
	[-CorsMaxAge <Integer>] 
	[-CorsAllowCredentials <Boolean>]
	[-AccessPolicyObjectIds <String[]>]
	[-SubscriptionId <String>]
	[-Tags <String[]>]  
	[-AsJob] 
	[-WhatIf] 
	[-Confirm] 
	[<CommonParameters>]
```
```powershell
Set-AzHealthcareApisFhirService
	-ResourceId <String>
	-HealthcareApisFhirServiceConfig <PSHealthcareApisFhirServiceConfig>
	[-SubscriptionId <String>]
	[-Tags <String[]>] 
	[-AsJob] 
	[-WhatIf] 
	[-Confirm] 
	[<CommonParameters>]
```
```powershell
Set-AzHealthcareApisFhirService
	[-InputObject <PSHealthcareApisFhirService>]
	[-Tags <String[]>]
	[-AsJob] 
	[-WhatIf] 
	[-Confirm] 
	[<CommonParameters>]
```

####Get-AzHealthcareApisFhirService
#####SYNOPSIS
Gets existing healthcareApis fhir service accounts created within the specified subscription or a resource group.

#####SYNTAX
```powershell
Get-AzHealthcareApisFhirService 
	-Name <String>
	[-ResourceGroupName <String>]
	[-SubscriptionId <String>]
	[<CommonParameters>]
```

```powershell
Get-AzHealthcareApisFhirService 
	-ResourceId <String>
	[<CommonParameters>]
```

####New-AzHealthcareApisFhirServiceCosmosDbConfig 
#### SYNOPSIS
Creates a new healthcareApis fhir service cosmosdbconfig object (PSHealthcareApisFhirServiceCosmosDbConfig).
#####SYNTAX
```powershell
New-AzHealthcareApisFhirServiceCosmosDbConfig 
	[-CosmosOfferThroughput <Integer>]
```
####New-AzHealthcareApisFhirServiceAuthenticationConfig
#####SYNOPSIS
Creates a new healthcareApis fhir service authenticationconfig object (PSHealthcareApisFhirServiceAuthenticationConfig).
#####SYNTAX
```powershell
New-AzHealthcareApisFhirServiceAuthenticationConfig
	[-Authority <String>] 
	[-Audience <String>] 
	[-SmartProxyEnabled <Boolean>]
```
####New-AzHealthcareApisFhirServiceCorsConfiguration 
#####SYNOPSIS
Creates a new healthcareApis fhir service corsConfig object (PSHealthcareApisFhirServiceCorsConfig).
#####SYNTAX
```powershell
New-AzHealthcareApisFhirServiceCorsConfig 
	[-CorsOrigins <String []>] 
	[-CorsHeaders <String[]>] 
	[-CorsMethods <String[]>] 
	[-CorsMaxAge <Integer>] 
	[-CorsAllowCredentials <Boolean>]
```
####New-AzHealthcareApisFhirServiceAccessPolicyEntry

#####SYNOPSIS
Creates a new healthcareApis fhir service accessPolicy entry object (PSHealthcareApisFhirServiceAccessPolicyEntry).

#####SYNTAX
```powershell
New-AzHealthcareApisFhirServiceAccessPolicyEntry 
	[-AccessPolicyObjectId <String>]
```

####New-AzHealthcareApisFhirServiceConfig 
#####SYNOPSIS
Creates a new healthcareApis fhir service config object (PSHealthcareApisFhirServiceConfig).
#####SYNTAX
```powershell
New-AzHealthcareApisFhirServiceConfig
	[-AccessPolicies <PSAccessPolicyEntry[]>] 
	[-AuthenticationConfig <PSAuthenticationConfig>]
	[-CosmosDBConfig <PSCosmosDBConfig>] 
	[-CorsConfig <PSCorsConfig>] 
```

### Changed cmdlet

`No Changed cmdlet`

## Guidelines

- Do your cmdlets comply with the design guidelines outlined in the [PowerShell Design Guidelines document](https://github.com/Azure/azure-powershell/tree/master/documentation/development-docs/design-guidelines)?
		yes

-	Do all applicable cmdlets follow the piping guideliens outlined in the Piping in PowerShell document?
		yes
