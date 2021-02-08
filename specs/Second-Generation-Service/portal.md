# Gen2 Portal 

The following document describes the design for the Gen2 Portal extension. 


# Scenarios

The new scenarios for the Gen2 Portal extension are:
* Support Workspaces
* Support DICOM services
* Allow multiple FHIR services under one Workspace
* Support SQL backed FHIR services 
* Continue supporting Gen1 during Gen2 Public Preview and GA

# Design

[Figma Diagram](https://microsoft.sharepoint.com/teams/ProjectResolute/Shared%20Documents/Forms/AllItems.aspx?id=%2Fteams%2FProjectResolute%2FShared%20Documents%2FGeneral%2FHealthcare%20APIs%20%2D%20Public%20Preview%2Epdf&parent=%2Fteams%2FProjectResolute%2FShared%20Documents%2FGeneral&p=true&wdLOR=c2D7157ED%2D3F71%2D4BC1%2D895C%2D41B5155DF1F9&ct=1610665738300&or=Outlook-Body&cid=A34870F9-B867-4A88-A907-B11F76AAD999&originalPath=aHR0cHM6Ly9taWNyb3NvZnQuc2hhcmVwb2ludC5jb20vOmI6L3QvUHJvamVjdFJlc29sdXRlL0VXQklHLVAxbFJwUG5jN2UwTUZmeDNBQjg5eTBhTkZfVi1VZGdHNXlNRV8xelE_cnRpbWU9MkRhRVl1RzQyRWc)

The Gen2 Portal extension will remain a single extension for all Azure Healthcare API offerings as under the workspace model all the services are created under one Azure resource. Divisions will be made within the codebase to allow individual service teams (FHIR, DICOM, IoT, ...) to own their sections of the extension.

All these services will share certain resources such as the Portal extension pdl files, external extensions used, icon folder, and text resource file.

Gen2 and Gen1 services will exist side by side as two seperate entities in Portal. They will have seperate browse experiences, and from a users point of view will not be connected.

During the Private Preview phase of Gen2 the Gen2 sections will be hidden behind a feature flag and use mock data.

All work for the Public Preview MVP must be done by March 17th to allow time for translation.

Notes from Ibiza meetings:<br>
December:
* Change IoT create to a side pop out blade instead of a full screen blade
* Add side menu to the IoT blades
* Change save/discard to form buttons at the bottom of the form
* Don't have editable forms launch other editable forms
* Have view/summary blades launch edit blades if multiple edit views are needed.

Febuary:
* Use a Pill control instead of a tab control for time selection on the metrics page. See the Activity Log blade for an example.
* Consider adding a Properties blade to align with other extensions.
* Send information to display on our summary hover card (seen when hovering over the app icon from the home page) to Balbir Singh.
* Investigate using a config generated create instead of our custom one.
* Changes to Create:
    * Tabs shouldn't show the required fields indicator
    * Change 'Location' to 'Region'
    * The Review pane should show a summary of Tags even when none are entered
    * Use summary controls on the Review pane for consistent styling
    * Previous and Next buttons should be present on every pane and enabled/disabled as needed

# Test Strategy

New E2E and unit test will be made to cover the new workflows.

In addition the current tests will be refactored to improve test relyability and ease of creation.<br>
Rework Unit Tests:
* Sandbox setup helper methods

Rework E2E Tests:
* Move to TS based framework
* Use Cucumber JS for easy to read files
* Make useable (they should be part of the PR build or at least CI)

# Stories
<b>----------------------------------- Public Preview MVP -----------------------------------</b><br>
Refactoring for Gen2
* Create Gen1, Gen2, and common folders for files
* Rename to use Healthcare instead of Fhir for shared components
* Add new ViewModel, Overview blade, and related settings in for Gen2
* Have the user directed to different Overview/Create blades based on generation.
  * Gen2 overview/create is just a copy of FHIR overview for now
* Hide Gen2 code in 
* Set up mock data for Gen2. If the feature flag is present no calls should be made to the backend.

Add Gen2 Terms
* Add records for new linguistic terms used by Gen2
  
Add Workspace
* Add Workspace create
* Show Workspace overview and menu
* Workspace overview is landing page for Gen2 services
  
Add FHIR server to Workspace (single FHIR server)
* Add FHIR server option to Workspace menu
* FHIR server option opens either:
  * FHIR create workflow if no server is linked to the workspace
  * Goes to FHIR overview if a server is linked to the workspace
* FHIR menu has new option to go to Workspace overview
* Breadcrumb is added when viewing FHIR server

Allow multiple FHIR servers to be created
* Change Workspace FHIR server blade to show list of FHIR servers linked to the workspace.
* Allow multiple FHIR servers to be created

Add DICOM to Workspace control level
* Add new Workspace menu option for DICOM blade
* DICOM blade lists DICOM servers the same as the FHIR balde lists FHIR servers
* Add DICOM create workflow (work with DICOM team to determine needs)
* Add DICOM overview blade
* Add DICOM menu with two options: Overview and Workspace

Add DICOM settings blades
* Add settings blades for DICOM settings (work with DICOM team to determine needs)

Add FHIR SQL settings
* Change FHIR Database blade to have settings for SQL server
* Add SKU choice to FHIR create workflow so users can select shared or dedicated SQL server

Modify FHIR Convert settings
* Change FHIR Convert to use registry/image/digest selection

Wire up to real data
* Before this all Gen2 data should be mocked

Review provided capabilities
* Check that capabilities provided by Portal are activiated as appropriate on services

Change Gallery text
* Add Gallery entry for Gen2
* Add text to reflect Gen2 in Public Preview

Review Translations
* Review translations for languages known by the team

Accessability review
* Review accessability and request a review from T&R Accessability

<b>----------------------------------- GA MVP -----------------------------------</b><br><br>
Move IoT to the Workspace control level
* IoT menu option added to Workspace menu
* Add controls to IoT create to allow selection of a FHIR server to link it to
