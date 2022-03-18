#Introduction
Currently, the Azure HealthCare API PaaS uses the metadata /certificate based authentication for ARM to RP authentication. However, there is a new requirment to migrate from metadata /certificate based authentication to AAD based authentication, max ETA on 06/30/2022

In this spec document, design of the AAD Arm to RP authentication/authorization for Health PaaS is presented

# Arm Manifest Changes
RP needs to update the manifest to include 'tokenAuthConfiguration' at the right scope to instruct ARM how to construct the token for authentication. Since we are going to enable the AAD authentication for every endpoint in the manifest, this configuration is specified at the manifest level as in the json snippet below

``` json
    "tokenAuthConfiguration": {
        "authenticationScheme": "PoP",
        "signedRequestScope": "Endpoint"
      }    
```
Inside the manifest looks like as in below
``` json
 {
    "providerAuthorizations": [
        {
            "applicationId": "caab7e9f-7737-4587-833a-c161d8ccf682"
        },
        {
            "applicationId": "3274406e-4e0a-4852-ba4f-d7226630abb7",
            "roleDefinitionId": "e39edba5-cde8-4529-ba1f-159138220220"
        },
        {
            "applicationid": "894b1496-c6e0-4001-b69c-81b327564ca4",
            "roleDefinitionId": "c69c1f48-8535-41e7-9667-539790b1c663"
        }
    ],
    "namespace": "Microsoft.HealthcareApis",
    "providerVersion": "2.0",
    "providerType": "Internal",

    "tokenAuthConfiguration": {
        "authenticationScheme": "PoP",
        "signedRequestScope": "Endpoint"
      },

    "requestHeaderOptions": {
        "optInHeaders": "NotSpecified"
    },
    "notificationOptions": "NotSpecified",
    "resourceGroupLockOptionDuringMove": {
        "blockActionVerb": "Action"
    },
    ----------------------------
 }
```
# Environment Group Updates
While the RP is validating the token from the Arm, it should use the same value of <i>tokenAuthConfiguration</i> as specified in the Arm manifest. In addition the library that RP team provides for authentication validation expects <i>appId</i> (<i>appGuid</i>) corresponding to the RP, for the purpose of logging. The following key-value pairs are required in RP code for validation of the token. These will be places in environment group json files and can be read into ```ServiceEnvironment ```  and be accessible within the RP worker
 ```json
 {
     -------------
       "firstPartyAppIds": {
        "azureRbac": "3274406e-4e0a-4852-ba4f-d7226630abb7",
        "apiForFhir": "4f6778d8-5aef-43dc-a1ff-b073724b9495"    // This will be the application Id(Guied) for RP
    },

    "aadAuthenticationEnabledWithFallback": true,   // will enable the AAD authorization with fallback to Certificate authroization
    "signedRequestScope": "Endpoint"
 }

 ```

 # ResourceProvider ApplicationManifiest Updates

 We need the following parameters in the applicationmanifest (Service Fabric) of the ResourceProviderWorker , that would later be accessible be via ```ServiceEnvironment```


 <b>AadAuthenticationEnabledWithFallback
 SignedRequestScope
 RpApplicationId</b>

# Code Changes
  
The ```ServiceEnvironment``` class will have the following new properties

   ```C# 
        public bool AadAuthenticationEnabledWithFallback { get; set; } = true;

        public string SignedRequestScope { get; set; } = "Endpoint";

        public string RpApplicationId { get; set; }
   ```

A new class ```AadAuthorizationWithCertificateFallbackHandler``` that implements ```Microsoft.Azure.ResourceProvider.Authorization.RequestAuthorizationHandlerBase``` will be added. The following code ( not complete) shows how the method ```ValidateRequestIsAuthorized``` will be overriden inside the new class ```AadAuthorizationWithCertificateFallbackHandler```
```C#
   public async override Task ValidateRequestIsAuthorized(IHttpRequest requestMessage)
        {
            try
            {
                await aadAuthorizationHandler.ValidateRequestIsAuthorized(requestMessage);
				// Metric to log the number of successful AAD authorization
            }
            catch (ResourceProviderException exception)
            {
				//Metric the number of failed AAD authorization/authentication

                traceContext.Warning(
                    new TraceMessage("Authorization via AAD failed. Falling back on Certificate authorization."),
                    exception);

                await certificateAuthoizationHandler.ValidateRequestIsAuthorized(requestMessage);
            }
            catch (Exception exception) // make sure this exception is thrown by the aadAuthorizationHandler
            {
                if (!exception.IsFatal())
                {
					//Metric the number of failed AAD authorization/authentication
                    traceContext.Warning(
                        new TraceMessage("Exception observed in AAD Authorization. Falling back on Certificate authorization."),
                        exception);

                    await certificateAuthoizationHandler.ValidateRequestIsAuthorized(requestMessage);
                }
                else
                {
                    traceContext.Error(
                        new TraceMessage("Fatal Exception observed, blocking execution."),
                        exception);

                    throw;
                }
            }
        }
    }
```
  
   The ```AMicrosoft.Health.Cloud.ARMResourceProvider.Service.Startup``` will have the following changes.

   ```C#
            if (serviceEnvironment.ArmAuthorizationEnabled)
            {
                if (serviceEnvironment.AadAuthenticationEnabledWithFallback)
                {
                    services.AddSingleton<IRequestAuthorizationHandler>((x)=> AadAuthorizationWithCertificateFallbackHandler(/**parameters **/))
                }
                else
                {
                    // existing certificate authorization
                }
               
            }
   ```

## Deployment 

As per the onboarding step(from RP), the new authentication/authorization code implemented in the PaaS should be deployed before the manifest change. Then the config value of ```AadAuthenticationEnabledWithFallback``` should be enabled(set to ```true```)


# Referencce
[RP documentation](https://armwiki.azurewebsites.net/api_contracts/ResourceProviderAADAuthentication.html)

