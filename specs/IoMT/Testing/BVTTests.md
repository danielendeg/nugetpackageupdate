*Summary of the feature.*

[[_TOC_]]

# Business Justification

To ensure the quality of Azure IoT Connector for FHIR releases, build verification tests must be developed to automate the process of ensuring bugs have not been introduced during a development cycle and to monitor the health of services in the regions supporting IoT Connector.

# Background

**Continuous Integration** - Currently, when Azure API for FHIR releases roll out into production, Integration, BVT, E2E and other tests are executed in each region. The process begins by provisioning test accounts using a script (Provision-TestAccounts.ps1). Tests are executed by calling the Run-FunctionalTests.ps1 script. If all tests pass, the regional deployment continues; if a test fails an alert notifying the team of the failure is emailed.

**Service Health Monitoring** - Every 3 hours, BVT and E2E tests are executed to monitor the health of the services in different regions and if a failure occurs an email alert is generated and sent to the team.

# Design

Modifications to 2 scripts enable IoT Connector BVT tests (and eventually E2E tests) for both Continuous Integration and Service Health Monitoring:

1. The Provision-TestAccounts.ps1 is modified to call Provision-TestIomtServices.ps1 after test accounts are provisioned. Two Iot Connectors are provisioned on the R4 test account in each region. The Provision-TestIomtServices.ps1 script saves primary connection strings to the global environment Key Vault once the IoT Connectors are provisioned.

1. The Run-FunctionalTests.ps1 is modified to call Run-IotConnectorFunctionalTests.ps1 after FHIR server functional tests are completed. Run-IotConnectorFunctionalTests.ps1 retrieves the primary connection string for each IoT Connector from Key Vault and set environment variables that will be used by the IoT Connector BVTs.

## Scripts

**Provision-TestIomtServices.ps1** - Handles provisioning new IoT Connectors for testing and saving connection strings to the global environment Key Vault. The script first checks for the existence of connectors by attempting to retrieve the primary connection strings. If the IoT Connectors are not found, they are deployed with device and FHIR mappings and connections using the **bvt-iomt-rg.json** ARM template. The outputs form the ARM template are the Iot Connector primary connection strings for each Connector.  

## Projects
**IotConnector.BVTTest** - A project where build verification tests are developed and curated. Tests can be executed using a Powershell script (Run-IotConnectorFunctionalTests.ps1) or through Visual Studio. Environment variables can be set in the script or by using a launchSettings.json file. The launchSettings.json file should be used when debugging tests through Visual Studio.

**IotConnector.TestUtilities** - A project containing various utilities that can be leveraged by tests. This includes an Event Hub client for sending messages to IoT Connectors, a client for creating and fetching Resources from a FHIR server, and test expectation utilities.

# Test Strategy

Test setup and teardown is handled by a test fixture (IotConnectorTestFixture.cs). The test fixture puts resources and sends all messages BEFORE all tests are executed. This is because the IoT Connectors could take up to 15 minutes to process the messages; rather than sending messages before each test, sending all messages in 1 or 2 batches before any test is executed will reduce the overall duration of the test run. After tests are executed, the resources created for the test run will be deleted from the FHIR server - except for the Patient and Device resource used for the "Lookup" IoT Connector - These resources can be reused between test runs. 

1. Two IoT Connectors will be deployed on an instance of Azure API for FHIR, one will use the "Lookup" Identity Resolution type, and the other will use "Create".

1. Patient and Device Resources will be uploaded to the FHIR server using the FHIR API. These Resources are used by the "Lookup" Connector and will only be created if they do not exist.

1. Messages will be sent to the Event Hubs for each IoT Connector simulating device data.

1. Resource creation will be verified.

# Test Cases

**Scenario Outline:** Sending a device message that is mapped results in the value saved to an Observation Resource.   
GIVEN an IoT Connector is deployed  
AND the Identity Resolution type is **\<resolution type\>**  
AND the mappings contain templates to handle the message  
AND the FHIR mapping value type is **\<value type\>** 
WHEN a valid device message is sent  
THEN an Observation containing the value exists in the FHIR server.

Examples:  
| **resolution type** | **value type** |  
| Lookup | SampleData |  
| Lookup | Quantity |  
| Lookup | String |  
| Create | Quantity |  
| Create | SampleData |  
| Create | String |  

The following properties will be used to validate an Observation resource was created as expected:

* codes
* patient reference
* device reference
* period
* SampledData unit, period, dimensions (SampledData type only)
* Quantity unit, code, system (Quantity type only)

**Scenario Outline:** Sending a device message that is mapped results in the creation of a Patient and Device Resource.   
GIVEN an IoT Connector is deployed  
AND the Identity Resolution type is Create
AND the mappings contain templates to handle the message
WHEN a valid device message is sent  
THEN a Patient and Device Resource are created.

# Other

* All test accounts (regardless of region) are deployed in the same subscription so the IoT Connector quota will need to be increased to ensure that 2 connectors per region can be deployed.

* IoT Connector test runs could add up to 15 minutes to deployments. This is because messages sent to an IoT Connector may take 15 minutes to be converted into FHIR Observations.  

* The first time the BVT infrastructure is provisioned, the deployment duration will increase approximately 10 minutes per region, as IoT Connectors will be provisioned into existing test accounts. Subsequent deployments should only increase by about 20 seconds.
