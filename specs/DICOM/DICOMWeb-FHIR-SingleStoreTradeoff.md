# DICOM Web on FHIR: Risks and Tradeoffs

## Background 

We have been exploring the potential of having a unified storage for DICOM and FHIR data represented as FHIR database and providing a DICOMWeb endpoint as an interface into that. This document contains some of the considerations for risks connected to developing and maintaining such service.

## Tradeoffs

### 1. Querying Instances and Series

As defined in section [6.7.1.1 Resource](http://dicom.nema.org/medical/dicom/current/output/chtml/part18/sect_6.7.html) of the DICOM web standard, it is a possible to search for series/ instances without a specific unique identifier at the study level. All FHIR resources are stored as an ImagingStudy resource, and therefore all FHIR level querying can __only__ be done at a study level.

All filtering for series/ instances will need to be done *client-side* after reducing the query to the specific study's FHIR ImagingStudy resources. However, this approach will make it significantly complex to support the paging functionality (**limit** and **offset**) as defined in the standard, when trying to accomplish this functionality using a combination of *server-side* and *client-side* filtering.

#### Initial Solution
We only support querying at a series/ instance level when the study instance identifier is provided. This will support most DICOM web viewers that tend to only do study level querying and filter down results based on a selected study (very similar to the DIMSE C-FIND approach). The limitation is not being able to support the less-standard queries e.g. "**Find all the series that have modality CT**". However, our recommendation here would be to attempt to run these queries using the FHIR interface.

Another limitation of this solution will be it's overall performance. Any client-side filtering might change the needs of the hosting server when querying at scale; this might add an over-head of cost to the end user. SKIP and LIMIT functionality would also be occurring *client-side*, introducing an extra network cost for each query, as entire studies will need to be fetched initially through the FHIR query.

This approach will also add more complexities for the DICOM web querying implementation. All queries will be implemented doing an initial ImagingStudy FHIR request, filtering the result, then fetching the associated DICOM metadata for each series/ instance to populate the result attributes.

Method|Path|Description
----------|----------|----------
*Search for Studies*|
GET|../studies?...|Search for studies|
*Search for Series*|
~~GET~~|~~../series?...~~|~~Search for series~~
GET|../studies/{study}/series?...|Search for series in a study
*Search for Instances*|
~~GET~~|~~../instances?...~~|~~Search for instances~~
GET|../studies/{study}/instances?...|Search for instances in a study
GET|../studies/{study}/series/{series}/instances?...|Search for instances in a series

> Competitor DICOM web implementations support querying without study instance identifier.

#### More Optimal Solution
For the DICOM query API it makes more sense to index data at an instance level. All querying should be done on instances, and to support study/ series level grouping, a `DISTINCT` could be run on the resulting instances to group the data (or a `GROUP BY`). This would allow a consistent approach to querying and reduce the over-head and complexities of mapping between *server-side* and *client-side* filtering.

### 2. Querying on non-FHIR Indexed Attributes
Querying on-top of FHIR resources will limit DICOM based queries to the attributes indexed in FHIR. As resources are stored at a study level (based on the FHIR standard), there will always be a limited number of attributes indexed at a series and instance level. This will restrict future versions from supporting more non-standard queries through the DICOM web interface (example, '*find all the patients scanned on the Philips CT scanner*').

#### Initial Solution
Add support for custom attributes in the FHIR imaging study resource. This will require significant engineering and effectively manipulate the FHIR resource to become a DICOM specific resource. A limitation here is that the custom attributes will only be known to the DICOM web server, and adding imaging study resources manually/ directly into the FHIR database will have an inconsistent level of support for the custom attributes.

#### More Optimal Solution
Indexed entire DICOM attributes at an instance level (minus large tags such as pixel data). This will be a highly extensible solution and could allow future support for querying using any DICOM tag at any study/ series/ instance level.

### 3. Dealing with DICOM blobs

Per DICOM paradigm, the DICOM files are immutable and if they are modified, a new DICOM SOP instance needs to be crated, with new UID and references to the original instance. Thus, we face a dilemma of either following the paradigm to the letter or risking the inconsistency in the data store

#### Initial Solution
We can run all search queries on FHIR datastore and server results from DICOM blob store. Customers may see that DICOM data has metadata that is different from what is stored in FHIR and this will be the expected documented behavior. 

#### More Optimal Solution
When updates to any FHIR resources are made, look for DICOM resources that may potentially be linked (keeping in mind that ImagingStudy may contain the link to the resource being modified, not the other way around) and make sure a secondary capture is created per standard.

## Risks

### 1. Performance

**Ingestion:** At scale, ingestion time will be degraded as we create, map, and resolve conflicts on ingestion. 

**Querying:** At scale, querying will have significant performance limitations. To resolve queries correctly, multiple lookups might be required to resolve a query. Once the DICOM web query is mapped to FHIR, multiple joins of separate resources might be needed to filter the data (joining patient and reference practitioner). After that, a secondary look-up is required to get the meta-data from the DICOM blob storage to resolve any required/ optional tags needed in the response dataset. As we will not be able map from FHIR to DICOM (we will only be able to map DICOM to FHIR), the metadata lookup is the only way to reliably return the correct response when querying on FHIR.

### 2. Querying Not on the Original DICOM data

If the DICOM query API runs all of its queries on the FHIR store, it is possible for the DICOM metadata to be out of sync with the FHIR data (external, direct updates to the FHIR store).
An example clinical risk here would be searching for all the imaging study resources where the patient name is **John Doe**. If an imaging study resource has been manually modified using the FHIR API to break a relationship between the imaging study and patient, the query API has the potential for returning a subset of the DICOM data that has been ingested. The behavior would not be possible if the DICOM API owned the data indexes for querying.

An initial request from a customer had the following quote:
> *"The solution should demonstrate storing the original data ingested in its original format as well as linking the original format to the longitudinal patient record via lineage/provenance recording as well as integrative association (original data format should be queryable at some level)"*

