# Support FHIR R5 in IoMT Connector

[[_TOC_]]

# 1 Background
We have supported FHIR R4 in the IoMT FHIR connector for both the PaaS and the OSS offering. We are planning to support the FHIR R5 in IoMT connector use cases.

We will deliver the OSS offering in the first wave once changes are ready. For the PaaS offering, it will depend on the official launch of the R5 by HL7 and the official launch of R5 support in the PaaS offering of the Azure FHIR API Service. A tentative timeline is Q4 of 2020.

## 1.1 Problem Statements
1. Enable the customer to use the OSS to create an IoMT connector with FHIR version R5. Customers can use three existing options 1) Create a connector with managed AAD 2) Create a connector with unmanaged AAD 3) Create end-to-end sandbox.
1. Enable the customer to use the PaaS to add the IoMT connector under the Add-in of the FHIR API service with FHIR version R5.

## 1.2 Assumptions
1. The downstream of the IoMT FHIR connector is the Azure FHIR API service or a web service that implements the FHIR with OAuth.
1. In the current architecture of the IoMT connector, only the transform function "MeasurementCollectionToFhir" is in the scope of FHIR dependency or is FHIR-mattered: its output data goes to the Azure FHIR API and must be FHIR conforming. The other components are FHIR independent. 
1. The FHIR does not guarantee backward compatibility all the time, e.g., there are minus changes in a new version. In the IoMT connector, when the measurement data does not conform to the selected FHIR version, the "MeasurementCollectionToFhir" should be able to detect it. The FHIR service that the connector talks to should validate the input data and return response following the FHIR API guideline for invalid input.
1. Though we only support selected data types in the FHIR mapping, we should always retain the feasibility to extend with more data types in the future. 
1. In the PaaS offering of Azure FHIR API service, one service instance can only serve one FHIR released version. Moreover, one Azure FHIR API Service instance, in both customer view and the internal view, is bound to one deployment of the runtime stack. If a customer wants to use multiple FHIR versions, they need to create multiple FHIR API instances.

## 1.3 Scopes
1. At this moment, we're targeting at FHIR R5 - Preview #2 - [version 4.4.0](http://hl7.org/fhir/2020May/). it will be updated when another build candidate is coming out for R5. All works scoped should be adaptive to those updates.

   - The impact of R5 on the Observation: 
     - Reference with [MedicationStatement](http://hl7.org/fhir/R4/medicationstatement.html) is removed. Reference with [MedicationUsage](http://build.fhir.org/medicationusage.html) is created.
     - Under "value", there is one more property added: "valueAttachment" of the datatype: [Attachment](http://build.fhir.org/datatypes.html#Attachment).

     Note, so far, we do not have MediationStatement as a part of measurement or FHIR mapping, neither the Attachment data type. The other data types remain unchanged. So with the R5, we will only need to make sure that there is no regression on the currently supported use cases.

     Reference: [Raw diff of R5 and R4](http://services.w3.org/htmldiff?doc1=http%3A%2F%2Fhl7.org%2Ffhir%2FR4%2Fobservation.html&doc2=http%3A%2F%2Fbuild.fhir.org%2Fobservation.html)

   - The impact of R5 on the Device and Patient:   
     There are internal structural changes and resource reference changes on Device, Patient and Encounter. In the LookUp mode, when a resource instance existed with the given ID, we will retrieve and load it in memory with the FHIR data model. So we need to make sure that the FHIR versions match between the Connector and the FHIR service; otherwise, there will be exceptions thrown for the non-conforming data structure. For example, the DisplayName of the Device resource type.  
     Reference: [Raw diff of Device between R5 and R4](http://services.w3.org/htmldiff?doc1=http%3A%2F%2Fhl7.org%2Ffhir%2FR4%2Fdevice.html&doc2=http%3A%2F%2Fbuild.fhir.org%2F%2Fdevice.html)    
     Reference: [Raw diff of Patient between R5 and R4](http://services.w3.org/htmldiff?doc1=http%3A%2F%2Fhl7.org%2Ffhir%2FR4%2Fpatient.html&doc2=http%3A%2F%2Fbuild.fhir.org%2F%2Fpatient.html)    
     Reference: [Raw diff of Encounter between R5 and R4](http://services.w3.org/htmldiff?doc1=http%3A%2F%2Fhl7.org%2Ffhir%2FR4%2Fencounter.html&doc2=http%3A%2F%2Fbuild.fhir.org%2F%2Fencounter.html)

2. When using the IoMT connector, the customer needs to specify an FHIR version for the IoMT connector. The customer also needs to choose an identity resolution type between "Create" and "LookUp". Currently, the ARM template uses one field to get the information for both the FHIR version information and the identity resolution type information. We will refactor it into two specific fields to accept the parameters from the customer.
    
## 1.4 User Story
Phase 0 - OSS offering:
1. [As a user of IoMT connector I'd like to deploy IoMT Connector OSS targeting an R5 FHIR Server.](https://microsofthealth.visualstudio.com/Health/_workitems/edit/74286)
1. [Create a new R5 FHIR Observation resource](https://microsofthealth.visualstudio.com/Health/_workitems/edit/74284)
1. [Update an existing R5 FHIR Observation resource](https://microsofthealth.visualstudio.com/Health/_workitems/edit/74285)
1. [Error Handling when Connecting with FHIR Server](https://microsofthealth.visualstudio.com/Health/_workitems/edit/74289)
 
Phase 1 - PaaS offering:     
Assume Connector's runtime behaviors are going to be similar as the OSS offering.

## 1.5 Goals
- Support FHIR version selection in a configurable way for the Connector
- Minimize architectural changes for the IoMT connector in both OSS and PaaS,
- The solution should be adaptive to extend FHIR mapping in R4, R5, or future versions, with minimum effort.
- Maintain the cost linear with a new FHIR version at least,
- No new security scope and boundaries extended other than new FHIR version dependency.

# 2 Metrics
1. The number of customers who used the R4/R5 deployment.
1. The number of deployment of a specific version.
1. The number of who succeeded in the past measuring period, e.g. last one hour. 

Note: for OSS, we currently use the tags on resources to collect metrics. It doesn't guarantee the accuracy since customer can edit/remove the tags before and after the deployment. However, it gives us the confidence at certain level for number of initial deployments, ratio of FHIR versions etc. 

# 3 Design
## 3.1 UX Spec
In this design, we will base off the existing architecture where an instance of IoMT connector only binds to one Azure FHIR API service endpoint. The endpoint is FHIR version-specific, so it means that one instance of the IoMT connector instance only supports one FHIR version. The lifecycle of a connector is independent of another one. (See Appendix for other options considered)

With the OSS offering, the customer needs to select an FHIR version on the template, providing the Azure FHIR API service endpoint with the same FHIR version, and choose an identity resolution type. 

For OSS solution, we'll add a new status check function or a module on the existing function to verify the FHIR version with the FHIR server based on its capability statement from the "/metadata" path. If the validation fails, we will fail early and fast with some signals sent to the customer. In the beginning, we can add instructions to let customer eyeball those monitors after deployment. And in the future, we can add more automatic alarming mechanisms. Additionally, we can also evaluate the approach to add scripts in the ARM deployment template to give more granular control in the deployment. It will depend on the [Deployment Script](https://docs.microsoft.com/en-us/azure/azure-resource-manager/templates/deployment-script-template?tabs=CLI) feature, which is in the Preview at this moment. However, the deployment script alone is not sufficient because we can't assume that the FHIR service is always available. Furthermore, the customer can also change the FHIR service endpoint in the Azure Function App's configuration after it deployed. A health check mechanism is still needed as it's sufficient to cover basic requirements of error handling and reporting at any time.

If the customer wants to use multiple FHIR versions, a new connector instance should get created with that version. When using the sandbox setup scripts, the customer will use a new optional argument -FhirVersion to specify the version they want. If not provided, the FHIR R4 will be default value to create the resource group with the template.

With the PaaS offering, the developer experience will not change. Under the hood, the connector creation will just use the FHIR version from the current context passed in from the FHIR service instance, which already tied to a specific FHIR version and start the connector deployment.

## 3.2 ARM Template Spec
For the OSS offering:
- We will add a new required parameter FhirVersion on the template in a dropdown of ["R4", "R5"]. The default value is R4. 
- we will change the "ResourceIdentityServiceType to a dropdown of ["Create", "LookUp", "LookUpWithEncounter(*tentative*)"]. It abstracts the strategy of creating and updating logic on the resource data. 
- we will extend the sandbox with an optional argument "FhirVersion". The default value is R4 when not specified.

## 3.3 Interface and Assembly Structure 
We will include all FHIR version-specific libraries in the "lib" folder of the solution. When deploying, the assemblies will get linked and compiled with libraries of a specific FHIR-version. (Please see the Appendix for other options considered)

One principle of marshaling and refactoring the current code base is to retain the FHIR impacting scope only to the transformation function. Generic transformation and ingestion logic should be in the sharable library project, and logics/services/wrappers that are version-specific should reside in the separate projects. The namespaces should keep consistent across the versions for common workflows. 

Therefore, we will keep the FHIR-version-free code or common workflows in high-level abstraction, and let the version-specific assemblies own the FHIR-version-mattered modules and control the FHIR versioning dependencies in the closure. A proposal to structure the project is below. The template parameter FhirVersion will get added to the "appsettings" section of "Microsoft.Web/sites" of the function. The parameter will get added to the Environment Variables list consumed in the function's lifecycle. Other less-favorable options: 1) fork the entry point assembly to version-specific which increase the blast radius of FHIR impact 2) pre-compile the assemblies and marshall them in web-packages on the cloud, in the template, it will just point to different locations to pull the version-specific packages and deploy those bytes with the version selected. This option introduces more work for us and seems not very necessary for an OSS project. And it also blocks customers from editing and experimenting with local changes. The resolute team adopted this solution for technical limitation reasons.

Structure Before:
```
func/Microsoft.Health.Fhir.Ingest.Host // Entry point
  ├── lib/Microsoft.Health.Fhir.Ingest
  └── lib/Microsoft.Health.Fhir.R4.Ingest

lib/Microsoft.Health.Fhir.Ingest
  ├── lib/Microsoft.Health.Common
  ├── lib/Microsoft.Health.Extensions.Host
  └── lib/Microsoft.Health.Extensions.Fhir

lib/Microsoft.Health.Common

lib/Microsoft.Health.Extensions.Fhir
  └── lib/Microsoft.Health.Common

lib/Microsoft.Health.Fhir.R4.Ingest
  ├── lib/Microsoft.Health.Common
  ├── lib/Microsoft.Health.Extensions.Host
  ├── lib/Microsoft.Health.Extensions.Fhir
  ├── lib/Microsoft.Health.Extensions.Fhir.R4
  ├── lib/Microsoft.Health.Fhir.Ingest
  └── Hl7.Fhir.R4

lib/Microsoft.Health.Extensions.Fhir.R4
  ├── lib/Microsoft.Health.Common
  ├── lib/Microsoft.Health.Extensions.Host
  ├── lib/Microsoft.Health.Extensions.Fhir
  └── Hl7.Fhir.R4  
```

Structure After:
```
func/Microsoft.Health.Fhir.Ingest.Host // Entry point
  ├── lib/Microsoft.Health.Fhir.Ingest
  └── lib/Microsoft.Health.Fhir.$(FhirVersion).Ingest // Differ on the selected FHIR Version

lib/Microsoft.Health.Fhir.Ingest
  ├── lib/Microsoft.Health.Common
  ├── lib/Microsoft.Health.Extensions.Host
  └── lib/Microsoft.Health.Extensions.Fhir

lib/Microsoft.Health.Extensions.Fhir
  └── lib/Microsoft.Health.Common

lib/Microsoft.Health.Common 

lib/Microsoft.Health.Fhir.R4.Ingest
  ├── lib/Microsoft.Health.Common
  ├── lib/Microsoft.Health.Extensions.Host
  ├── lib/Microsoft.Health.Extensions.Fhir
  ├── lib/Microsoft.Health.Extensions.Fhir.R4
  ├── lib/Microsoft.Health.Fhir.Ingest
  └── Hl7.Fhir.R4

lib/Microsoft.Health.Extensions.Fhir.R4
  ├── lib/Microsoft.Health.Common
  ├── lib/Microsoft.Health.Extensions.Host
  ├── lib/Microsoft.Health.Extensions.Fhir
  └── Hl7.Fhir.R4

lib/Microsoft.Health.Fhir.R5.Ingest
  ├── lib/Microsoft.Health.Common
  ├── lib/Microsoft.Health.Extensions.Host
  ├── lib/Microsoft.Health.Extensions.Fhir
  ├── lib/Microsoft.Health.Extensions.Fhir.R5
  ├── lib/Microsoft.Health.Fhir.Ingest
  └── Hl7.Fhir.R5

lib/Microsoft.Health.Extensions.Fhir.R5
  ├── lib/Microsoft.Health.Common
  ├── lib/Microsoft.Health.Extensions.Host
  ├── lib/Microsoft.Health.Extensions.Fhir
  └── Hl7.Fhir.R5
```

For the PaaS option, we will modify the build process to iterate the FHIR versions and generate different web packages stored on the remote repo. In the control plane, when the customer triggered the connector creation, the web package with the selected version will be pulled and deployed. 

# 4 Test Strategy
- All existing and new unit tests should pass,
- Manually test that the IoMT connector can be deployed with selected version,
- Test IoMT connector in LookUp mode with DeviceID that has the Device Resource in R5 preset on the FHIR server, especially testing the new and modified fields in R5 resource data structure, for example, DisplayName in Device or SubjectStatus in Encounter,
- Test IoMT connector in Create mode without the Device Resource on the FHIR server,
- Test both OSS and PaaS solutions.

# 5 Security
There is no additional security concern introduced at this point except using the open-source project Hl7.Fhir.R5 that will get imported from Nuget. The other component and boundaries remain the same.

# 6 Appendix
## 6.1 Other Options Considered
### **Version Management Strategy**
Option list:
- #1 (**Preferred**) Manage the FHIR versions by segregating and linking assemblies that are FHIR and FHIR version-specific    
  Pros:
  - FHIR version impacting scope is only transformation function at this moment, it shouldn't affect the release cycle of the entire solution.
  - Much easier dev Ops and maintenance cost for us. We will have less number of integration workflow/pipelines to ensure the availability of each version.
  - Given the possibility we may want to run multiple FHIR version connector, this option doesn't create a one-way door.    

  Cons:
  - the assemblies list will go long when supporting more FHIR versions
  - the refactoring complexity will increase when a new FHIR version introduces breaking changes to us or we will need version-specific feature.
  - Not too scary but still higher complexity of deprecating a supported FHIR version. 
- #2 Manage the FHIR versions by branching and tagging the repo of microsoft/iomt-fhir

  Pros:
  - Less complexity of abstraction and refactor in adding and removing FHIR version support.
  - Less footprints for customers who just interested in one FHIR version. 

  Cons:
  - More efforts in DevOps with more pipeline, releasing cycles to create and monitor.
  - Increase the blast of radius of a component's dependency handling to the entire solution. The branches will get messy and merge will get harder and harder when permute the FHIR version with other potential solution level branching strategies. 
  - The above impacts both cloud and local dev and release. 

The option #1 is preferred at this moment. We expect to support only 2 or 3 FHIR versions at most for seen future, given R5 is not officially released yet and it usually takes unit of year to release a new version in the past history. Furthermore, we want to remain focus on the Preview and GA goal, so we should limit the changing areas to keep it stable as much as possible.

### **UX Options for handling the FHIR version**
Option list: 
- #1 Creation of IoMT connector instance per FHIR version.    
  In this option, one IoMT connector instance is tied to one FHIR service endpoint and FHIR version. To use another FHIR service endpoint or FHIR version, the customer needs to create a new connector instance.
- #2 Configurating FHIR versions and FHIR service endpoints in an IoMT connector instance with automatic routing.  
  In this option, the customer can configure the connector instance to hook up multiple FHIR service endpoints in multiple FHIR versions. Customers need to provide multiple templates when they wanted to handle any FHIR version-specific data structure. As for now, the resource types and data types we exposed to the customer are interchangeable between R4 and R5; however, that may change in the future for adopting deltas between versions.

The second option is tempting and desired in improvements and optimization of current experience. It may bring down a lot of costs for customers and us when dealing with more FHIR version bumping ups. On the other side, it also introduces many redesign, refactoring, re-testing, and re-evaluating works. We can evaluate the impact when we have more customer data. In the current phase, we will prioritize the first option at this moment.

## 6.2 FAQ
1. Q: Why do we need to work on R5 support even it's not officially published yet?
A: There are a couple of reasons to start this effort:
    1) The FHIR Server already supported R5 in the OSS offering. Eventually, the IoMT Connector should be consistent and support the newer version R5. We should start early and set up baselines for delivering R5 supports in phases.
    2) We take this opportunity to investigate the diff between R4 and R5, planning early for potential breaking changes or new features we eager to adopt.
    3) We evaluate a few options for longer-term engineering excellence.
    4) We use this opportunity to refactor and optimize code structure for extending newer FHIR versions.

1. Q: What FHIR Resource types and Data types we are using in the template?   
A: Supported types:
   - Resource Type: 
     - Lv3: Patient, Device, Encounter
     - Lv4: Observation
   - Data Type:
     - Primitive: [String - N](http://build.fhir.org/datatypes.html#string),
     - Complex:  [Quantity - N](http://build.fhir.org/datatypes-definitions.html#Quantity), [SampledData - TU](http://build.fhir.org/datatypes-definitions.html#SampledData), [CodeableConcept - N](http://build.fhir.org/datatypes-definitions.html#CodeableConcept)

1. Q: What are changes for R5 vs R4 so far?   
   A: 
   | version | changes |
   |---|---|
   | 4.4.0 Preview#2 | - **Trial Use:** Further Development of the Subscription framework (SubjectStatus, rename Topic to SubscriptionTopic)<br> - **Trial Use:** Add new Resources Permission, NutritionProduct, Citation, and EvidenceFocus |
   | 4.2.0 Preview#1 | - **Normative:** Rework the abstact data types (new types Base, DataType, BackboneType), and introduce CanonicalResource and MetadataResource interfaces<br> - **Trial Use:** Major upgrade to Subscription resource and the pub/sub patterns |
   http://hl7.org/fhir/2020May/history.html
        

1. Q: What is the GA date for the PaaS offering of FHIR API service with R5?
   A: The GA data for the PaaS offering of R5 will go after both the official launch of R5 by the HL7 and the official launch of R5 by the Azure FHIR API service.


1. Does Kudu and App Service support NetCore 3.1 now?   
   A: Yes. https://github.com/projectkudu/kudu/issues/3138

## 6.3 References
- FHIR API spec (used by the Resolute team): https://www.hl7.org/fhir/http.html
- FHIR version handling guideline: https://www.hl7.org/fhir/versions.html#change
