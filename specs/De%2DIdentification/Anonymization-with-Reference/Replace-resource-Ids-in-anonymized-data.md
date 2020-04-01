[[_TOC_]]

# Business Justification
Resource Ids are PIIs. Therefore, user would like to have an option to replace the resource ids consistently across FHIR documents so that the original resource ids are not shared yet references are properly maintained. 

This is a very important anonymization requirement. Without having this capability, the user will need to remove the resource ids in order to protect the PII. And, removing resource ids will make it difficult to join the referencing resources together; and hence make the data less useful.

The ability to join the resources is fundamental to this requirement.

# Background 
## Resource Id
Each resource has an "id" element which contains the logical identity of the resource assigned by the server responsible for storing it. 
Logical ids (and therefore literal identities) are case sensitive. 

Ids can be up to 64 characters long, and contain any combination of upper and lowercase ASCII letters, numerals, "-" and ".".

**Regex**: ```[A-Za-z0-9\-\.]{1,64}```

## Reference
In a resource, references are represented with a reference (literal reference), an identifier (logical reference), and a display (text description of target).

At least one of reference, identifier and display SHALL be present (unless an extension is provided).

> Reference.reference: A reference to a location at which the other resource is found. The reference may be a relative reference, in which case it is relative to the service base URL, or an absolute URL that resolves to the location where the resource is found. The reference may be version specific or not. If the reference is not to a FHIR RESTful server, then it should be assumed to be version specific. Internal fragment references (start with '#') refer to contained resources.

## Compatibility with FHIR server
FHIR server accepts resource ids of regex ```[A-Za-z0-9\-\.]{1,64}```. If our new resource Ids are conformed the this regex, the anonymization result will work in FHIR server.

# Design
This work can be divided two parts: resolving resource Ids and replaceing resource Ids.

## Resolving resource Ids
Resource Ids may exists in *"Resource.id"* field and *"Reference.reference"*.

Resource Ids in *"Resource.id"* are simply string following the required regex ```[A-Za-z0-9\-\.]{1,64}```.

Resource Ids may also exist in literal references *"Reference.reference"* in the format of an absolute URL, a relative URL or an internal fragment reference.
1. Absolute URLs shall follow the following regex if the reference to a resource is consistent with a FHIR API:
```
 ((http|https):\/\/([A-Za-z0-9\-\\\.\:\%\$]*\/)+)?(Account|ActivityDefinition|AdverseEvent|AllergyIntolerance|Appointment|AppointmentResponse|AuditEvent|Basic|Binary|BiologicallyDerivedProduct|BodyStructure|Bundle|CapabilityStatement|CarePlan|CareTeam|CatalogEntry|ChargeItem|ChargeItemDefinition|Claim|ClaimResponse|ClinicalImpression|CodeSystem|Communication|CommunicationRequest|CompartmentDefinition|Composition|ConceptMap|Condition|Consent|Contract|Coverage|CoverageEligibilityRequest|CoverageEligibilityResponse|DetectedIssue|Device|DeviceDefinition|DeviceMetric|DeviceRequest|DeviceUseStatement|DiagnosticReport|DocumentManifest|DocumentReference|EffectEvidenceSynthesis|Encounter|Endpoint|EnrollmentRequest|EnrollmentResponse|EpisodeOfCare|EventDefinition|Evidence|EvidenceVariable|ExampleScenario|ExplanationOfBenefit|FamilyMemberHistory|Flag|Goal|GraphDefinition|Group|GuidanceResponse|HealthcareService|ImagingStudy|Immunization|ImmunizationEvaluation|ImmunizationRecommendation|ImplementationGuide|InsurancePlan|Invoice|Library|Linkage|List|Location|Measure|MeasureReport|Media|Medication|MedicationAdministration|MedicationDispense|MedicationKnowledge|MedicationRequest|MedicationStatement|MedicinalProduct|MedicinalProductAuthorization|MedicinalProductContraindication|MedicinalProductIndication|MedicinalProductIngredient|MedicinalProductInteraction|MedicinalProductManufactured|MedicinalProductPackaged|MedicinalProductPharmaceutical|MedicinalProductUndesirableEffect|MessageDefinition|MessageHeader|MolecularSequence|NamingSystem|NutritionOrder|Observation|ObservationDefinition|OperationDefinition|OperationOutcome|Organization|OrganizationAffiliation|Patient|PaymentNotice|PaymentReconciliation|Person|PlanDefinition|Practitioner|PractitionerRole|Procedure|Provenance|Questionnaire|QuestionnaireResponse|RelatedPerson|RequestGroup|ResearchDefinition|ResearchElementDefinition|ResearchStudy|ResearchSubject|RiskAssessment|RiskEvidenceSynthesis|Schedule|SearchParameter|ServiceRequest|Slot|Specimen|SpecimenDefinition|StructureDefinition|StructureMap|Subscription|Substance|SubstanceNucleicAcid|SubstancePolymer|SubstanceProtein|SubstanceReferenceInformation|SubstanceSourceMaterial|SubstanceSpecification|SupplyDelivery|SupplyRequest|Task|TerminologyCapabilities|TestReport|TestScript|ValueSet|VerificationResult|VisionPrescription)\/[A-Za-z0-9\-\.]{1,64}(\/_history\/[A-Za-z0-9\-\.]{1,64})?
```
2. A relative URL is relative to a base URL and mostly in the format of [```[type]/[id]```](https://www.hl7.org/fhir/bundle.html#references)
3. An internal fragment reference describes referencing a contained resource and is in the format of ```#[id]```. For a resource that references the container, the reference is "#".
We will resolve resource Ids from *"Reference.reference"* by matching the above patterns.

## Replacing resource Ids
We investigated two approaches to replace resource Ids: the Mapping Table approach and the Encryption approach.

### Mapping Table approach
We might just replace all the resource id with a new GUID and maintain a mapping table for transforming all references. Customers can save the mapping file for re-Id purpose.

**Pros**
1. The transformation is very straightforward.
2. If we preserve the mappings between the original resource id and the new resource id, we will be able to support re-identification, which is a future requirement.

**Cons**
1. We have to maintain a mapping table. [ @<8ED32720-FC34-6AEA-9795-3EE47CE9512B> , Do you have any thought on how we can scale out?  As the data volume increases, there will be a need to process resources in parallel, possibly on different machines. It is also possible that the same resource id is getting processed at the same time at different machines]


### Encryption approach
We might encrypt all ids with a key given by our customer. The encrypted id is the new ID we want. There are little dependencies as we only need a key and can preserve the key for re-identification purpose with symmetric encryption method.

But after our investigation, cypher texts are mostly longer than input texts (AES CBC, AES ECB, 3DES) or equal to input texts length (AES CFB). If we encode the characters of cypher bytes in 6 bits to confirm to accepted characters in ```[A-Za-z0-9\-\.]{1,64}```, [ @<8ED32720-FC34-6AEA-9795-3EE47CE9512B> Since the characters are in 6 bit to begin with, how about we first map those to 8 bit, cypher it, and then map it back to 6 bits?] the cypher texts will be even longer and exceed the length limit of 64 bytes. Thus, we decide not to adopt this approach.

## Conclusion
We will extract resource Ids from *"Resource.id"* field and *"Reference.reference"*, replace resource Ids with new GUID confirmed to FHIR requirement and preserve a mapping table from old resource Ids to new resource Ids.


