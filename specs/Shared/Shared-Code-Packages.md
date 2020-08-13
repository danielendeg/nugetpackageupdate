# Sharing common code
The goal is to share common code between the [`fhir-server`](https://github.com/microsoft/fhir-server) and the [`dicom-server`](https://github.com/microsoft/dicom-server) with as little disruption as possible. 

tldr; [The plan](#plan) is to move the common code to a new GitHub repo and create Nuget packages for the common libraries that the [`fhir-server`](https://github.com/microsoft/fhir-server) and [`dicom-server`](https://github.com/microsoft/dicom-server) consume.

[[_TOC_]]

## Background
There are a number of code packages that currently live within the [`fhir-server`](https://github.com/microsoft/fhir-server) repo that would be of use in the [`dicom-server`](https://github.com/microsoft/dicom-server) and potentially any other future open source offerings by our teams. Today, these fall into two main areas:

### SQL management
Each of the servers has a SQL backend that must take care to manage its schema. This functionality includes the code to host the schemas, the tool to upgrade schemas, and the code for a server to register its use of a particular schema. 

### Extensions
A number of extensions exist today mainly in the area of dependency injection that would be useful in all solutions.

## Options
### Nuget packages
The approach of pre-building the code from a common repo and exposing the code as a series of packages hosted via Nuget. 

#### Pros 
* Clear boundaries between projects.
* Well established, common workflow.
* Explicit reference to specific version of the common library.

#### Cons
* Harder to debug. 
* While code churn is high, this approach is the least attractive (but code churn should be low once the schema management is fully implemented).
* Updates must be checked-in, built, and published.
    * Will need an easy way to test the changes in the target project. (could possibly use Jack's existing script for the fhir -> paas path.)

### git submodules
Git submodules are a way to link multiple repositories together in the same folder structure. You can read more about git submodules [in the git book](https://git-scm.com/book/en/v2/Git-Tools-Submodules) and [from Atlassian](https://www.atlassian.com/git/tutorials/git-submodule).

#### Pros
* Source for the common library is available in the same folder structure.
* Local builds will just work
* Specific commit reference

#### Cons
* Requires a different workflow with the use of `git submodule init` and `git submodule update`.
* Points to specific commit hashes that need to be updated
* The git commands for branching/pushing have to be executed against the submodules

### git subtrees
Git subtrees are a somewhat similar approach to submodules, but the code lives more within the directory. You can read more about git subtrees [from Atlassian](https://www.atlassian.com/git/tutorials/git-subtree) and [from GitHub](https://help.github.com/articles/about-git-subtree-merges/).

#### Pros
* Source for the common library is available in the same folder structure.
* Local builds will just work

#### Cons
* Requires a different workflow 
* Contributing code to the common library is more complicated
* Puts onus on developer to correctly merge commits. 

### monorepo
This approach would be to combine all of the repos together into a single large repository. 

#### Pros
* References and code versioning is all handled together.
* Local builds will just work.

#### Cons
* Not easy to create new services without announcing them.
* CI/CD/Packaging needs to be handled differently than it is today.
* New projects will need to use this repo and if not, then we have the same options as above.

# Plan
Create a new repo [`healthcare-shared-components`](https://github.com/microsoft/healthcare-shared-components) and put the common projects inside. Each project will remain independent and be published as separate packages, but will have their version incremented in step. Publication of these projects as nuget packages will be done via the [MicrosoftHealthOss Azure Devops Nuget  feed](https://microsofthealthoss.visualstudio.com/FhirServer/_packaging?_a=feed&feed=Public). PRs will be built and tested using GitHub actions. CI and Nuget publishing will be handled via Azure Devops pipelines.

## Migration plan
1. Make new repo for [`healthcare-shared-components`](https://github.com/microsoft/healthcare-shared-components).
1. Copy and update one of the common projects.
1. Verify that [sourcelink](https://github.com/dotnet/sourcelink) is enabled for the project.
1. Add command to quickly test nuget in a consuming repo. 
1. Create release pipeline for this repo that publishes nuget packages.
1. Ingest the package back into the [`fhir-server`](https://github.com/microsoft/fhir-server) repo and remove the original source.
1. Repeat the process (2, 3, and 6) for other common projects.

## Developer Responsibilities
* If the code isn't a breaking change
    * The person making the change is responsible for verifying this in the known referencing projects
* If the code is a breaking change
    * The person should work with the corresponding teams on a publishing/rollout plan
* Ideally there would be an automated build that could verify these changes