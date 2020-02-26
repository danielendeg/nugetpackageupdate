# Motivation

Traditionally, all medical imaging data is stored as DICOM files. Imaging files contain pixel data and a rich set of meta-data that describe:
1. The type of imaging procedure performed
1. DateTime of the procedure
1. The patient subject
1. Who performed and requested the study/series
1. The software and hardware used to create and process image data

We wish to merge this information with FHIR's ImagingStudy resource, to create a single query plane for combined DICOM and EHR records.

```
NB The mapping outlined here between DICOM and FHIR is based on the latest DICOM standard. In practice not only is it almost impossible to find data that exercises these mappings, it should also be expected that much of this mapping cannot be done as tags are not there or different vendors/hospitals use different schema. It is highly likely that all of this schema will need to change for individual customers, based on careful analysis of their data.   
```
## DICOM
All DICOM CIODs (Composite Information Object Definitions) share the same basic Entity-Relationship model as defined in the [specfication](ftp://dicom.nema.org/medical/Dicom/current/output/chtml/part03/chapter_A.html#sect_A.1.2). As defined, all CIODs, follow the
* Patient
    * Study
        * Series

And thus fall naturally into FHIRs ImagingStudy resource. 

### DICOM CIOD
Each CIOD is expressed as a set of "modules". Within a CIOD a module is defined as either
* M (Mandatory)
* C (Conditional)
* U (User Option)

Each module comprises a set (tree) of tags, where each tag is either
* 1 (Requried with a value)
* 2 (Requried but value can be empty if unknown)
* 3 (Optional)

The requirement level of a tag can also be conditional on some other aspect of the data. In this case 'C' is suffixed to the level.  

It is important to note that individual tags can be specified in different modules with different requirements. e.g. The [MR Image CIOD](https://dicom.innolitics.com/ciods/mr-image) contains 2 mandatory modules [General Image](https://dicom.innolitics.com/ciods/mr-image/general-image) and [MR Image](https://dicom.innolitics.com/ciods/mr-image/mr-image) that both contain the tag ImageType. Within [General Image](https://dicom.innolitics.com/ciods/mr-image/general-image/00080008) it is type 3 (optional) but within [MR Image](https://dicom.innolitics.com/ciods/mr-image/mr-image/00080008) it is type 1 (Required). 

### Value Multiplicity
Each tag within a module, has an associated value multiplicity specifying the number of permissable elements within the tag value. e.g. [CT Image CIOD, CT Image Module, Image Type](https://dicom.innolitics.com/ciods/ct-image/ct-image/00080008) has a value multiplicity of 2-n. Meaning there must be at least 2 code values defined. 

### Value Representation
A Value Representation of a DICOM tag is how values are stored, the full list is [here](http://dicom.nema.org/dicom/2013/output/chtml/part05/sect_6.2.html). Each DICOM tag has a fixed value representation regardless of which module it is in. Note that some VRs require special parsing such as DT, DS and do not fit naturally into json [number](http://www.json.org/) format.

### Caveat Lector
DICOM software and hardware manufacturers can also specify private dictionaries in their conformance statements. These could include valuable information for generating ImagingStudies, this is not considered in this specification. It has also been known for vendors to specify different semantics for tags in the CIODs they generate. 

## FHIR
FHIR contains and ImagingStudy resource that we wish to populate with appropriate information from an ingested set of DICOM files. Within STU3 the [ImagingStudy](http://hl7.org/fhir/STU3/imagingstudy.html) resource is defined as:

```json
{
  "resourceType" : "ImagingStudy",
  // from Resource: id, meta, implicitRules, and language
  // from DomainResource: text, contained, extension, and modifierExtension
  "uid" : "<oid>", // R!  Formal DICOM identifier for the study
  "accession" : { Identifier }, // Related workflow identifier ("Accession Number")
  "identifier" : [{ Identifier }], // Other identifiers for the study
  "availability" : "<code>", // ONLINE | OFFLINE | NEARLINE | UNAVAILABLE
  "modalityList" : [{ Coding }], // All series modality if actual acquisition modalities
  "patient" : { Reference(Patient) }, // R!  Who the images are of
  "context" : { Reference(Encounter|EpisodeOfCare) }, // Originating context
  "started" : "<dateTime>", // When the study was started
  "basedOn" : [{ Reference(ReferralRequest|CarePlan|ProcedureRequest) }], // Request fulfilled
  "referrer" : { Reference(Practitioner) }, // Referring physician
  "interpreter" : [{ Reference(Practitioner) }], // Who interpreted images
  "endpoint" : [{ Reference(Endpoint) }], // Study access endpoint
  "numberOfSeries" : "<unsignedInt>", // Number of Study Related Series
  "numberOfInstances" : "<unsignedInt>", // Number of Study Related Instances
  "procedureReference" : [{ Reference(Procedure) }], // The performed Procedure reference
  "procedureCode" : [{ CodeableConcept }], // The performed procedure code
  "reason" : { CodeableConcept }, // Why the study was requested
  "description" : "<string>", // Institution-generated description
  "series" : [{ // Each study has one or more series of instances
    "uid" : "<oid>", // R!  Formal DICOM identifier for this series
    "number" : "<unsignedInt>", // Numeric identifier of this series
    "modality" : { Coding }, // R!  The modality of the instances in the series
    "description" : "<string>", // A short human readable summary of the series
    "numberOfInstances" : "<unsignedInt>", // Number of Series Related Instances
    "availability" : "<code>", // ONLINE | OFFLINE | NEARLINE | UNAVAILABLE
    "endpoint" : [{ Reference(Endpoint) }], // Series access endpoint
    "bodySite" : { Coding }, // Body part examined
    "laterality" : { Coding }, // Body part laterality
    "started" : "<dateTime>", // When the series started
    "performer" : [{ Reference(Practitioner) }], // Who performed the series
    "instance" : [{ // A single SOP instance from the series
      "uid" : "<oid>", // R!  Formal DICOM identifier for this instance
      "number" : "<unsignedInt>", // The number of this instance in the series
      "sopClass" : "<oid>", // R!  DICOM class type
      "title" : "<string>" // Description of instance
    }]
  }]
}
```
# Mapping
The [FHIR ImagingStudy Mappings](http://hl7.org/fhir/STU3/imagingstudy-mappings.html) propose the following mappings between DICOM and ImagingStudy:
|||||
|--- |--- |--- |--- |
|FHIR|Mapping Tag| Notes | DICOM Tag Type |
|ImagingStudy|Reference IHE radiology TF vol 2 table 4.14-1| 
|uid|(0020,000D)|StudyInstanceUID | 1 |
|accession|(0008,0050)| AccessionNumber| 2|
|identifier|(0020,0010)| StudyId | 2|
|availability|| Config |
|modalityList|(0008,0061)| C-FIND result not relevant Construct by hand|
|patient|(0010/*)| Need to defined a resource mapping, or construct|
|context||
|started|(0008,0020)+(0008,0030)| StudyDate StudyTime - need to be careful with timezone| 2 |
|basedOn||
|referrer|| Could use (0008,0090) + (0008,0096) ReferringPhysician, need to query for Practitioner Resource| 2 |
|interpreter|(0008,1060)| Name of physician reading study, query for Practioner Resource | 3 |
|endpoint|| Need to create/query Endpoint resource |
|numberOfSeries|(0020,1206)| Not relevant - query result, construct by hand |
|numberOfInstances|(0020,1208)| Not relevant - query result, construct by hand |
|procedureReference|(0008,1032)| Resource Reference query based on ProcedureCodeSequence | 3|
|procedureCode|| Snomed Code, define config mapping on (0008,1032) ? | 3|
|reason|| [See codes](http://hl7.org/fhir/STU3/valueset-procedure-reason.html)
|description|(0008,1030)| StudyDescription | 3 |

At the series level:
|||||
|--- |--- |--- |--- |
|series||
|uid|(0020,000E)| SeriesInstanceUID | 1 |
|number|(0020,0011)| SeriesNumber | 2 |
|modality|(0008,0060)| Modality | 1 |
|description|(0008,103E)| Series Description | 3 |
|numberOfInstances|(0020,1209)| Not Relevant C-FIND result, Count by hand|
|availability|(0008,0056)| Config |
|endpoint|| Only set if merging with a study with a different EndPoint? |
|bodySite|(0018,0015)| BodyPartExamined Need to translate to SNOMED Code |  3 |
|laterality|(0020,0060)|Laterality Convert to SNOMED| 2C |
|started|(0008,0021) + (0008,0031)| SeriesDate SeriesTime - careful with UTC conversion | 3 |
|performer|(0008,1050) | (0008,1072)| Performing Physician - look up Practioner reference | 3 |
Instance level:
|||||
|--- |--- |--- |--- |
|instance||
|uid|(0008,0018)| SOPInstanceUID | 1 |
|number|(0020,0013)| InstanceNumber | 2 |
|sopClass|(0008,0016)| SOPClassUID | 1 |
|title|(0070,0080) or (0040,A043) > (0008,0104) or (0042,0010) or (0008,0008)| Modality specific

# Constructing ImageStudy resources
## SOP Class Filtering
We will provide configuration options to define the accepted SOPClasses. See: [The current list of standard SOP Classes](ftp://dicom.nema.org/medical/Dicom/current/output/chtml/part04/sect_B.5.html)

## Mapping
We propose the following mapping schema to map DICOM to FHIR.
## Study Level
### Fixed Mappings
Fixed mappings reflect the DICOM entity-relationship. All instances must have at least the following tags to be accepted. 
||||
|--- |--- |--- |
|FHIR Path| DICOM Tag | Notes |
|```uid```|StudyInstanceUID|Must be present and non-empty else instance rejected|
|```availibility```| Set to ONLINE for all ImagingStudy resources created by this conolidator|
|```endpoint```| Always set to the id of DICOM wado-rs Endpoint defined in the Config|

### Optional
Other FHIR parameters are filled in if the same value is found across all study instances.
If discrepencies exist the entire study is rejected. (TODO: config options for this)
||||
|--- |--- |--- |
|```accession```| AccessionNumber ||
|```identifier```| StudyID ||
|```started```| StudyDate+StudyTime ||
|```description```| StudyDescription ||
|
### Derived
||||
|--- |--- |--- |
|```modalityList```| Modality |The unique list of modalities of series within the study |
|```numberOfSeries```| Count of series within study |
|```numberOfInstances```| Total count of SOPInstances (NB not images/frames!) within the study|
|
### Resource/parameter Look Up
Note that for the 1st version we will provide a code interface to return resource references so this can be adapted with clear boundaries. Note that implementations may also choose to return references to resources they create.
||||
|--- |--- |--- |
|```patient```| A configuration option to return a single Reference to a Patient Resource based on all tags in DICOM group 10 (0010,*) | |
|```referrer```| A configuration option to return a single Reference to a Practioner Resource based on (0008,0090) and sequence (0008,0096) ||
|```interpreter```| A configuration option to return 0-n References to a Practioner Resource based on (0008,1060) and sequence (0008,1062) ||
|```procedureReference```| A configuration option to return 0-n References to a Procedure Resource based on (0008,1032)|
|```procedureCode```| A configuration option to return 0-n CodeableConcepts based on sequence (0008,1032)|
|```reason```| A configuration option to return 0-1 CodeableConcepts based on sequence  (0040,1012)|

## Series Level
### Fixed Mappings
||||
|--- |--- |--- |
|FHIR Path| DICOM Tag | Notes |
|```uid```|SeriesInstanceUID||
|```modality```|Modality|
|```availability```| Always set to ONLINE |
|```endpoint```| Only set if the configured endpoint is different from the parent study endpoint|
### Optional
||||
|--- |--- |--- |
|FHIR Path| DICOM Tag | Notes |
|```number```| SeriesNumber ||
|```description```| SeriesDescription ||
|```bodySite```| Fixed translation of BodyPartExamined into the equivalent SNOMED Code, if the tag is present and the translation exists|
|```laterality```|Fixed translation of Laterality into the equivalent SNOMED Code , if the tag is present and the translation exists|
|```started```| SeriesDate+SeriesTime ||
|```description```| SeriesDescription ||
### Derived
### Resource/parameter Look Up
||||
|--- |--- |--- |
|```performer```|A configuration option to return 0-n References to a Practioner Resource based on Operator's Name (0008,1070) and the Sequence (0008,1072)|

## Instance Level
### Fixed Mappings
||||
|--- |--- |--- |
|```uid```| SOPInstanceUID |
|```sopClass```| sopClassUID |
### Optional
||||
|--- |--- |--- |
|```number```| InstanceNumber (0020,0013) |
|```title```| If Encapsulated PDF or Encapulated CDA - (0042,0010), If Image - ImageType (0008,0008), If Presentation State - (0070,0080)|

# Consolidaton with existing resources
During ingestion, we always attempt to construct a valid (source) ImagingStudy with associated reference resources. We may need to merge these resources with an existing (target) ImagingStudy, we make the following assumptions:
1. We always ingest complete series (we will never update series with partial results)

## Study Level
We choose the following policies when there is an existing (target) ImagingStudy:
1. The Referenced ```patient``` must be the same in source and target, else the entire study is rejected.
1. The ```accession``` parameter must be the same if present in both source and target. If 1 is not present, we update the merged result.
1. The ```identifier``` parameter must be the same if present in both source and target. If 1 is not present, we update the merged result.
1. ```started``` must be the same if present in both source and target. If 1 is not present, we update the merged result
1. ```referrer``` must be the same if present in both source and target. If 1 is not present, we update the merged result.
1. ```reason``` must be the same if present in both source and target. If 1 is not present, we update the merged result.
1. ```description``` must be the same if present in both source and target. If 1 is not present, we update the merged result.

The following attributes are merged to form a unique list in target, source order:
1. ```interpreter``` 
1. ```procedureReference```
1. ```procedureCode```

If the ```endpoint``` differs from the target endpoint, we keep the target endpoint, and include the ingestion endpoints at the series.endpoint level.

## Series Level
At the Series Level we always overwrite Series with the same ```uid```

## Post Consolidation
After Consolidation we recompute all ```derived``` values listed above.

# Updating the FHIR database
It may be possible that multiple users may attempt to update the same ImagingStudy concurrently. If the ImagingStudy cannot be committed to the FHIR database, the Consolidation process will be re-tried n times using an exponential fall off pattern. 

# Rejecting instances
The Ingestion process, will provide a detailed error report for all rejected SOPInstances. 

# Creating other resouces

DICOM may also contain sufficient information that allows creation of other resources referenced in an ImagingStudy. In the above, we plan to provide interfaces for retrieving referenced resources based on specific groups of DICOM tags that may be present in an ingested set of DICOM files and note that implementors could return references to resources they create.

We outline possible mapping for referenced resources below.

## Patient resource creation

The [FHIR Patient](http://hl7.org/fhir/stu3/patient.html) resource is defined below:
(For brevity we have removed non-human aspects)
```json
{
  "resourceType" : "Patient",
  // from Resource: id, meta, implicitRules, and language
  // from DomainResource: text, contained, extension, and modifierExtension
  "identifier" : [{ Identifier }], // An identifier for this patient
  "active" : <boolean>, // Whether this patient's record is in active use
  "name" : [{ HumanName }], // A name associated with the patient
  "telecom" : [{ ContactPoint }], // A contact detail for the individual
  "gender" : "<code>", // male | female | other | unknown
  "birthDate" : "<date>", // The date of birth for the individual
  // deceased[x]: Indicates if the individual is deceased or not. One of these 2:
  "deceasedBoolean" : <boolean>,
  "deceasedDateTime" : "<dateTime>",
  "address" : [{ Address }], // Addresses for the individual
  "maritalStatus" : { CodeableConcept }, // Marital (civil) status of a patient
  // multipleBirth[x]: Whether patient is part of a multiple birth. One of these 2:
  "multipleBirthBoolean" : <boolean>,
  "multipleBirthInteger" : <integer>,
  "photo" : [{ Attachment }], // Image of the patient
  "contact" : [{ // A contact party (e.g. guardian, partner, friend) for the patient
    "relationship" : [{ CodeableConcept }], // The kind of relationship
    "name" : { HumanName }, // A name associated with the contact person
    "telecom" : [{ ContactPoint }], // A contact detail for the person
    "address" : { Address }, // Address for the contact person
    "gender" : "<code>", // male | female | other | unknown
    "organization" : { Reference(Organization) }, // C? Organization that is associated with the contact
    "period" : { Period } // The period during which this contact person or organization is valid to be contacted relating to this patient
  }],
  "communication" : [{ // A list of Languages which may be used to communicate with the patient about his or her health
    "language" : { CodeableConcept }, // R!  The language which can be used to communicate with the patient about his or her health
    "preferred" : <boolean> // Language preference indicator
  }],
  "generalPractitioner" : [{ Reference(Organization|Practitioner) }], // Patient's nominated primary care provider
  "managingOrganization" : { Reference(Organization) }, // Organization that is the custodian of the patient record
  "link" : [{ // Link to another patient resource that concerns the same actual person
    "other" : { Reference(Patient|RelatedPerson) }, // R!  The other patient or related person resource that the link refers to
    "type" : "<code>" // R!  replaced-by | replaces | refer | seealso - type of link
  }]
}
```

DICOM group (0010,*) contains overlapping patient information. [See relevant sections of the current DICOM standard](http://dicom.nema.org/medical/dicom/current/output/chtml/part03/sect_C.2.2.html). Again we have removed elements relating to non-human subjects. It is also not clear in clinical practice how much of this information will be present. 

|||||
|--- |--- |--- |--- |
|||||
|(0010,0010)|PN|```Patient's Name```|2|
|(0010,0020)|LO|```Patient ID```|2|
|(0010,0021)|LO|```Issuer of Patient ID```|3|
|(0010,0024)|SQ|```Issuer of Patient ID Qualifiers Sequence```||
|(0010,0030)|DA|```Patient's Birth Date```|2|
|(0010,0032)|TM|Patient's Birth Time|3|
|(0010,0033)|LO|Patient's Birth Date in Alternative Calendar||
|(0010,0034)|LO|Patient's Death Date in Alternative Calendar||
|(0010,0035)|CS|Patient's Alternative Calendar||
|(0010,0040)|CS|```Patient's Sex```|2|
|(0010,0050)|SQ|Patient's Insurance Plan Code Sequence||
|(0010,0101)|SQ|```Patient's Primary Language Code Sequence```| RFC 4646 |
|(0010,0102)|SQ|```Patient's Primary Language Modifier Code Sequence```||
|(0010,0200)|CS|Quality Control Subject||
|(0010,0201)|SQ|Quality Control Subject Type Code Sequence||
|(0010,1000)|LO|```Other Patient IDs```||
|(0010,1001)|PN|```Other Patient Names```||
|(0010,1002)|SQ|```Other Patient IDs Sequence```|3|
|(0010,1002).(0010,0022)|CS|```Type of Patient ID```|3.1|
|(0010,1005)|PN|Patient's Birth Name||
|(0010,1010)|AS|Patient's Age||
|(0010,1020)|DS|Patient's Size||
|(0010,1021)|SQ|Patient's Size Code Sequence||
|(0010,1030)|DS|Patient's Weight||
|(0010,1040)|LO|Patient's Address||
|(0010,1060)|PN|Patient's Mother's Birth Name||
|(0010,1080)|LO|Military Rank||
|(0010,1081)|LO|Branch of Service||
|(0010,1090)|LO|Medical Record Locator||
|(0010,1100)|SQ|Referenced Patient Photo Sequence||
|(0010,2000)|LO|Medical Alerts||
|(0010,2110)|LO|Allergies||
|(0010,2150)|LO|Country of Residence||
|(0010,2152)|LO|Region of Residence||
|(0010,2154)|SH|```Patient's Telephone Numbers```||
|(0010,2155)|LT|```Patient's Telecom Information```||
|(0010,2160)|SH|Ethnic Group||
|(0010,2180)|SH|Occupation||
|(0010,21A0)|CS|Smoking Status||
|(0010,21B0)|LT|Additional Patient History||
|(0010,21C0)|US|Pregnancy Status||
|(0010,21D0)|DA|Last Menstrual Date||
|(0010,21F0)|LO|Patient's Religious Preference||
|(0010,2203)|CS|Patient's Sex Neutered||
|(0010,2297)|PN|```Responsible Person```||
|(0010,2298)|CS|```Responsible Person Role```||
|(0010,2299)|LO|```Responsible Organization```||
|(0010,4000)|LT|Patient Comments||

Possible mapping values are highlighted. 

We propose the following mapping schema for patients:

## Fixed mappings
|||||
|--- |--- |--- |--- |
|FHIR|DICOM|Notes|
|||||
| ```identifier```|  (0010,0020) + (0010,0022) + (0010,0021)| Note that [Identifier type](https://dicom.innolitics.com/ciods/xrf-image/patient/00101002/00100022) in DICOM does not map onto [FHIRs](http://hl7.org/fhir/stu3/valueset-identifier-type.html) defintions. It is not clear how to map Issuer to FHIR's Identifier type. What about other IDs?|


## Optional mappings
These tags must be consistent across a set of SOPInstances relating to the same patient. (Config | Discuss)

|||||
|--- |--- |--- |--- |
|FHIR|DICOM|Notes|DICOM type|
|||||
| ```name```| (0010,0010) | Straight-forward translation of PN value representation to HumanName | 2 |
| ```gender```| (0010,0040) |FHIR Administritive gender vs DICOM gender (MFO) | 2 |
| ```birthdate```| (0010,0030)||
| ```address```| (0010,1040) | Basic representation in address.text|
| ```contact```| (0010,2297)|
| ```communication```| (0010,0101)| Mapping from RFC 4646 => ISO-639-1 alpha?||
| ```communication.preferred```| Note that (0010,0101) is in preference order |

## Stuff that is too ambigous

|||||
|--- |--- |--- |--- |
|FHIR|DICOM|Notes|DICOM type|
|||||
|[telecom](http://hl7.org/fhir/stu3/datatypes.html#ContactPoint)| (0010,2155) | 
|```photo```| Not obvious how to map (0010,1100)||

## Stuff we will not map
|||||
|--- |--- |--- |--- |
|FHIR||||
|||||
|```active```||||
|```active```||||
|```deceasedBoolean```||||
|```deceasedDateTime```||||
|```maritalStatus```||||
|```multipleBirthBoolean```||||
|```multipleBirthInteger```||||
|```generalPractitioner```||||
|```managingOrganization```||||
|```link```||||

# Practitioner Resource Creation

```json
{
  "resourceType" : "Practitioner",
  // from Resource: id, meta, implicitRules, and language
  // from DomainResource: text, contained, extension, and modifierExtension
  "identifier" : [{ Identifier }], // A identifier for the person as this agent
  "active" : <boolean>, // Whether this practitioner's record is in active use
  "name" : [{ HumanName }], // The name(s) associated with the practitioner
  "telecom" : [{ ContactPoint }], // A contact detail for the practitioner (that apply to all roles)
  "address" : [{ Address }], // Address(es) of the practitioner that are not role specific (typically home address)
  "gender" : "<code>", // male | female | other | unknown
  "birthDate" : "<date>", // The date  on which the practitioner was born
  "photo" : [{ Attachment }], // Image of the person
  "qualification" : [{ // Qualifications obtained by training and certification
    "identifier" : [{ Identifier }], // An identifier for this qualification for the practitioner
    "code" : { CodeableConcept }, // R!  Coded representation of the qualification
    "period" : { Period }, // Period during which the qualification is valid
    "issuer" : { Reference(Organization) } // Organization that regulates and issues the qualification
  }],
  "communication" : [{ CodeableConcept }] // A language the practitioner is able to use in patient communication
}
```

Within the Patient Resource, Practitioners are optionally referenced through the following attributes:
1. referrer
1. interpreter
1. series.performer

These equate to the optional DICOM tags:
1. (0008,0090) + (0008,0096) ReferringPhysician (General Study)
2. (0008,1060) + (0008,1062) Name of Pyhsician(s) reading study (General Study)
3. (0008,1070) + (0008,1072) Operator's Name (General Series)

Fortunately (0008,0096), (0008,1062) and (0008,1072) are all instances of the [Person Identification Macro Attributes](http://dicom.nema.org/medical/dicom/current/output/chtml/part03/chapter_10.html#table_10-1) with the following properties:

|||||
|--- |--- |--- |--- |
|(0040,1101) |Person Identification Code Sequence | 1 |
|(0040,1102) |Person's address| 3 |
|(0040,1104) |Person's Telephone numbers| 3 |
|(0040,1104) |Person's Telecom Information| 3 |
|(0008,0080) |Institution Name | 1C |
|(0008,0081) |Institutions Address | 3 |
|(0008,0082) |Institution Code sequence | 1C |

Both (0040,1101) and (0008,0082) are examples of the [Code Sequence Macro Attributes](http://dicom.nema.org/medical/dicom/current/output/chtml/part03/sect_8.8.html#table_8.8-1)

We will use the following mapping:

## Fixed Mappings:
|||||
|--- |--- |--- |--- |
| ```identifier``` | (0040,1101).(0008,0100) |  How to create the appropriate system? (Must be present)|
|```name```| (0008,0090) as appropriate to Practitioner instance | 

## Optional Mappings
|||||
|--- |--- |--- |---|
|```address``` | (0040,1102) | Simple mapping | 

