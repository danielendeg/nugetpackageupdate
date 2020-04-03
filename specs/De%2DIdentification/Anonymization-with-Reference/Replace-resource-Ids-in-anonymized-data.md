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
\
<span style="color:green">Update 1. @<356939D1-F4CA-6BA1-875C-7247D42D7353> reference URLs contain logical [OIDs or UUIDs](https://www.hl7.org/fhir/references.html#Reference) that resolve within the transaction
\
Oid Regex: ```urn:oid:(?<id>[0-2](\.(0|[1-9][0-9]*))+)```
\
Uuid Regex: ```urn:uuid:(?<id>[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})```
</span>

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
4. Update 2 @<356939D1-F4CA-6BA1-875C-7247D42D7353>  <span style = "color:green"> For both Uuid and Oid, we will resolve resolve Ids from the regex above directly. But for Oid, since it's in another format of id, we will replace the format in Uuid, that is, a reference of ```urn:oid:1.2.3.4.5``` will be transformed to ```urn:uuid:069e1601-005a-4886-b5cd-864cc5bf12e1```, where 069e1601-005a-4886-b5cd-864cc5bf12e1 is a mapped UUId of 1.2.3.4.5. By this way we solved the format mismatch between Oid and Uuid and kept the reference chain between resources.</span>
## Replacing resource Ids
We investigated two approaches to replace resource Ids: the Mapping Table approach and the Encryption approach.

### Mapping Table approach
We might just replace all the resource id with a new GUID and maintain a mapping table for transforming all references. Customers can save the mapping file for re-Id purpose.

**Pros**
1. The transformation is very straightforward.
2. If we preserve the mappings between the original resource id and the new resource id, we will be able to support re-identification, which is a future requirement.
3. FHIR server preserves resource id / reference as GUID. If we transform the Ids to a GUID, there will not be a confirmance problem.

**Cons**
1. We have to maintain a mapping table. [ @<8ED32720-FC34-6AEA-9795-3EE47CE9512B> , Do you have any thought on how we can scale out?  As the data volume increases, there will be a need to process resources in parallel, possibly on different machines. It is also possible that the same resource id is getting processed at the same time at different machines]

> Good point as we may process the resources in distributed mode. I will look on this scenario again and give an update on this.
 Update 3 @<356939D1-F4CA-6BA1-875C-7247D42D7353> <span style = "color:green"> As for processing in parallel, the anonymizer tool will still be working in a single machine. For parallel in a distributed mode, a possible solution is we retain a mapping file accross different machines, and union all mapping files together, where a resource id "patient1" may have multiple mapping result GUIDs (same as machine Count), we can also complete the re-identification process with this 1-n mapping relationship. [ @<8ED32720-FC34-6AEA-9795-3EE47CE9512B> If we have multiple resource ids for the same physical resource, it will be a huge problem for the consumer of the de-identified data. For example, for the same patient John Smith, there will be multiple resource ids. So, a data scientist will not be able to get all the data related to John smith.] [@<356939D1-F4CA-6BA1-875C-7247D42D7353>, @<8ED32720-FC34-6AEA-9795-3EE47CE9512B>, Apart from anonymization inefficiency, it might be a lot of overhead for customer to manage the mapping files (especially if multiple  anonymization jobs are used for multiple consumers). I am not sure how this will get managed if we ever support request/response scenario (each bundle having separate mapping file). Moreover, mapping won't be stable across different jobs unless user passes previous mapping file (think of anonymization of incremental data : _since)]
@<7AEC8627-72FE-4CC7-8062-C348124CA707> @<356939D1-F4CA-6BA1-875C-7247D42D7353>  Yes, I recognized these constrainsts and agree that mapping file seems could not meet distributed requirements. Should we consider utilizeing a centric Id server like a redis instance to help store the reference context between workers?

### Encryption approach
We might encrypt all ids with a key given by our customer. The encrypted id is the new ID we want. There are little dependencies as we only need a key and can preserve the key for re-identification purpose with symmetric encryption method.

But after our investigation, cypher texts are mostly longer than input texts (AES CBC, AES ECB, 3DES) or equal to input texts length (AES CFB). If we encode the characters of cypher bytes in 6 bits to confirm to accepted characters in ```[A-Za-z0-9\-\.]{1,64}```, [ @<8ED32720-FC34-6AEA-9795-3EE47CE9512B> Since the characters are in 6 bit to begin with, how about we first map those to 8 bit, cypher it, and then map it back to 6 bits?] the cypher texts will be even longer and exceed the length limit of 64 bytes. Thus, we decide not to adopt this approach.

Update 4 @<356939D1-F4CA-6BA1-875C-7247D42D7353> < Some further investigation results:
1. For now, same length encryption of AES implementation are not supported in dotnet Core. What's worse, the encryption works in a padding mode when input string is not meeting the desired multiple block size (usually 16bytes), we should utilize padding for id length of 63 bytes. And for multiple block size like 64, encryption will add another padding block. Even we can shrink the input to 48 bytes, the padding will break the length limit too ( I have thinked over the compress to 6 bits input, we still facing the padding problem as we cannot feed in byte array of 6 bits and padding to the end will bring risks to the encrytion result. It is not a good practise to hack encrytion algorithms.
In a word, we did'nt find a proper encryption way to work this out. 
2. As reference can be in format of UUid/ Oid,  there will be more limitations with the transformed Id (confirmed to a UUID format if it was a UUID). **The format restriction (character regex & length limit) is really a bottleneck for us**.
[@<356939D1-F4CA-6BA1-875C-7247D42D7353>, @<8ED32720-FC34-6AEA-9795-3EE47CE9512B>, what is more important : runtime preference (dotnet core vs something else) or feature usability? We should also question the strength requirement of the encryption algo (given a bad intention can identify data with various attacks as mentioned in spec). Comments/Thoughts?]
@<7AEC8627-72FE-4CC7-8062-C348124CA707>  Sorry for the confusion. It's mainly for the feature usability issue. I have discussed with my collegues who have rich encryption experience. Finding an encyption algorithm that can transform resource Id to a new Id confirmed to FHIR requirements is very hard.
Specifically,
* If we take the input string in bytes, we can manage to get a cypher text of the same length, (1-64 bytes), but the characters in the output cannot  confirm to the allowed char sets if [A-Za-z0-9\-\.].
* If we take the input string in a compressed mode of 6 bits ([A-Za-z0-9\-\. which contains 64 characters can be encoded in 6 bits), then we can compress the input bit stream to origin_length * 3 / 4; as the origin_length varies in [1, 64], we have to pad the input as the bit stream can not be converted to a input byte array, padding will introduce another issue that there maybe conflicts which means we may need to add another padding for those bit lengths of multiple 8 bytes. This will exceed the length limit. Or we have to develop or find an encryption that accepts bit stream that works by bit instread of byte.
Again, the encryption will be good if it's not important to keep the new Id format confirmed to FHIR requirements.

# Conclusion
We will extract resource Ids from *"Resource.id"* field and *"Reference.reference"*, replace resource Ids with new GUID confirmed to FHIR requirement and preserve a mapping table from old resource Ids to new resource Ids.


