Service isolation for health-paas engineering tools.

[[_TOC_]]

# Business Justification

Through a survey and interviews to veteran and new Health Cloud and Data developers, a common source of friction for development in the `health-paas` repo is the current usability of localbuildanddeploy.ps1. To better align its scenarios with current developer expectations, this one-pager outlines some refactoring, focusing on separation of concerns and re-usability between local developers and pipelines.

In addition to usability, developers are asking that failures in one area/service do not interrupt validation and deployment of other areas/services. 

# Scenarios
1. Per-service local build and deploy to personal environments.
   - A developer making changes to a specific service (e.g. Resource Provider, FHIR server) only wants to build and deploy relevant components to the service.
2. Seperation of concerns in CI and CD pipelines.
   - CI and deployment pipelines for the `health-paas` repo should be isolated to specific services.

# Non-Goals
- Deploying infrastructure and applications locally (i.e. gen1 local debugging)

# Design
- Split localbuildanddeploy.ps1 functionality into into two scripts, one for build and unit tests, and one for deployment and integration tests.
- Generalize the scripts well enough to be used as entrypoints in engineering

## build.ps1

### Example Parameters
- `Services`, The service to perform actions on (e.g. All, FHIR, ResourceProvider, erc).
- `Restore`, Perform a NuGet restore of the specified service(s).
- `Build`, Perform a build of the specified service(s).
- `UnitTests`, Run the unit tests for the specified service(s).
- `Package`, Package the specified service(s).
- `BuildConfiguration`, Set the build configuration (default: Release)
- `Verbosity`, Set the dotnet tool verbosity (default: minimal)

### Example Usage
- Restores, builds, run unit tests, and packages all services.
  
  `.\build.ps1 -Services All -Restore -Build -UnitTests -Package`

- Builds and runs unit tests for the FHIR services.

  `.\build.ps1 -Services FHIR -Build -UnitTests`

## deploy.ps1

### Example Parameters
- `EnvironmentName`, The unique identifier for the environment.
- `Region`, The region for the environment.
- `DeployInfrastructure`, Switch to deploy the environment infrastructure.
- `DeploySQL`, Switch to deploy SQL deployment during when deploying environment infrastructure.
- `DeployApplication`, Applications to deploy (e.g. ResourceProvider, FHIR server, etc)
- `Provision`, Components to provision (e.g. WorkspaceFhir, DICOM, Gen2Iot)
- `IntegrationTests`, Integration tests to run (e.g. E2E, DICOM, IoT)
   - _Note that we will further extend the functionality of targeting integration tests in the future._
- `Verbosity`, Set the dotnet tool verbosity (default: minimal)

### Example Usage
- Deploys the infrastructure, RP and FHIR applications, and provisions WorkspaceFHIR and DICOM test accounts in West US 2. Then runs DICOM integration tests.

  `.\deploy.ps1 -EnvironmentName adribona -Region wus2 -DeployInfrastructure -DeployApplication ResourceProvider,FHIR -Provision WorkspaceFhir,DICOM -IntegrationTests DICOM `

## Engineering Pipelines Integration
Many of the `health-paas` pipelines use existing powershell scripts that are well-factored for pipeline scenarios. Though, these existing scripts can be difficult to discover and understand when to use when developing locally and using personal environments.
1. Ensure new build/deploy.ps1 adequately support engineering pipeline scenarios.
2. Update engineering build and deployment pipelines to leverage common build/deploy scripts (i.e. build.ps1 and deploy.ps1). This will ensure there is proper validation of script changes as part of running the engineering pipelines.
3. Leverage seperations-of-concern in new scripts to enable per-service isolation in the engineering pipelines. This should allow us to further parallelize much of the CI, CD, and testing down to the service-level for engineering pipelines.

# Test Strategy
Many of the `health-paas` pipelines use existing powershell scripts that are well-factored for pipeline scenarios. Though, these existing scripts can be difficult to discover and understand when to use when developing locally and using personal environments.

# Security
No additional security features are required.
