# DICOM Conformance Statement

> This is currently a work-in progress document
> 

The **Azure for Health API** supports a subset of the DICOM Web standard. Support includes:

- [Store (STOW-RS)](##Store-(STOW-RS))
- [Retrieve (WADO-RS)](##Retrieve-(WADO-RS))
- [Search (QIDO-RS)](##Search-(QIDO-RS))

Additionally, the following non-standard APIs are supported:

- [Delete](##Delete)

## Retrieve (WADO-RS)
Web Access to DICOM Objects (WADO) enables you to retrieve specific studies, series and instances by reference. The specification for WADO-RS can be found in [PS3.18 6.5](http://dicom.nema.org/medical/dicom/2019a/output/chtml/part18/sect_6.5.html). WADO-RS can return binary DICOM instances, as well as rendered instances.

The **Azure for Health API** supports the following **HTTP GET** endpoints:

Method|Path|Description|Accept Header
----------|----------|----------|----------
DICOM|
GET|../studies/{study}|Retrieve full study|application/dicom
GET|../studies/{study}/series/{series}|Retrieve full series|application/dicom
GET|../studies/{study}/series/{series}/instances/{instance}|Retrieve instance|application/dicom
Metadata|
GET|../studies/{study}/metadata|Retrieve full study metadata|application/dicom+json
GET|../studies/{study}/series/{series}/metadata|Retrieve full series metadata|application/dicom+json
GET|../studies/{study}/series/{series}/instances/{instance}/metadata|Retrieve instance metadata|application/dicom+json

- Accept Header Supported: `application/dicom`

## Store (STOW-RS)

Store Over the Web (STOW) enables you to store specific instances to the server.
**Important** STOW-RS will only support storing entire series. If the same series is posted multiple times the behaviour is to override.

Method|Path|Description
----------|----------|----------
POST|../studies|Store instances
POST|../studies/{studyInstanceUID}|Store instances for a specific study. If any instance does not belong to the studyInstanceUID it will be rejected

- Accept Header Supported: `application/dicom+json`
- Content-Type: `multipart/related; type=application/dicom`

### Response

Code|Name|Description
----------|----------|----------
200 | OK | When all the SOP instances in the request have been stored
202 | Accepted | When some instances in the request have been stored
409 | Conflict | When none of the instances in the request have been stored

- Content-Type: `application/dicom+json`
- DicomDataset:
  - Retrieve URL (0008,1190)
  - Failed SOP Sequence (0008,1198)
    - Referenced SOP Class UID (0008,1150)
    - Referenced SOP Instance UID (0008,1155)
    - Failure Reason (0008,1197)
  - Referenced SOP Sequence (0008,1199)
    - Referenced SOP Class UID (0008,1150)
    - Referenced SOP Instance UID (0008,1155)
    - Retrieve URL (0008,1190)

### Dicom store semantics

- Stored DICOM files should at least have the following tags:
  - SOPInstanceUID
  - SeriesInstanceUID
  - StudyInstanceUID
  - SopClassUID
  - PatientID
- If the same SOP instance is stored multiple times we will override with the latest

## Search (QIDO-RS)

Query based on ID for DICOM Objects (QIDO) enables you to search for studies, series and instances by attributes. More detail can be found in [PS3.18 6.7](http://dicom.nema.org/medical/dicom/2019a/output/chtml/part18/sect_6.7.html).

The **Azure for Health API** supports the following **HTTP GET** endpoints:

Method|Path|Description
----------|----------|----------
*Search for Studies*|
GET|../studies?...|Search for studies|
*Search for Series*|
GET|../series?...|Search for series
GET|../studies/{study}/series?...|Search for series in a study
*Search for Instances*|
GET|../instances?...|Search for instances
GET|../studies/{study}/instances?...|Search for instances in a study
GET|../studies/{study}/series/{series}/instances?...|Search for instances in a series

Accept Header Supported: `application/dicom+json`

### Supported Query Parameters
The following parameters for each query are supported:

Key|Support Value(s)|Allowed Count|Description
----------|----------|----------|----------
`{attributeID}=`|{value}|0...N|Search for attribute/ value matching in query.
`includefield=`|`{attributeID}`<br/>'`all`'|0...N|The additional attributes to return in the response.<br/>When '`all`' is provided, please see [Search Response](###Search-Response) for more information about which attributes will be returned for each query type.<br/>If a mixture of {attributeID} and 'all' is provided, the server will default to using 'all'.
`limit=`|{value}|0..1|Integer value to limit the number of values returned in the response.<br/>Value can be between the range 1 >= x <= 100.
`offset=`|{value}|0..1|Skip {value} results.<br/>If an offset is provided larger than the number of search query results, a 204 (no content) response will be returned.

#### Search Parameters
We support searching on any attribute defined in the DICOM instances. We also support different behaviours based on the value representation of the tag.

Search Type|Supported Value Representation(s)|Example|Description
----------|----------|----------|----------|----------
Range Query|DA (Date)<br/>DT (Date Time)<br/>TM (Time)|{attributeID}={value1}-{value2}|For date/ time values, we supported an inclusive range on the tag. This will be mapped to `attributeID >= {value1} AND attributeID <= {value2}`.
Exact Match|AE (Application Entity)<br/>AS (Age String)<br/>AT (Attribute Tag)<br/>CS (Code String)<br/>DA (Date)<br/>Decimal String (DS)<br/>DT (Date Time)<br/>FL (Floating Point Single)<br/>FD (Floating Point Double)<br/>IS (Integer String)<br/>LO (Long String)<br/>LT (Long Text)<br/>PN (Person Name)<br/>SH (Short String)<br/>SL (Signed Long)<br/>SS (Signed Short)<br/>ST (Short Text)<br/> TM (Time)<br/>UI (Unqiue Identifer - UID)|{attributeID}={value1}|This is a straight-forward exact match of the element value. As some DICOM tags have a value multiplicity greater than 1, where applicable, the search will check all values using an `ARRAY_CONTAINS`.

#### Attribute ID

Tags can be encoded in a number of ways for the query parameter. We have partially implemented the standard as defined in [PS3.18 6.7.1.1.1](http://dicom.nema.org/medical/dicom/2019a/output/chtml/part18/sect_6.7.html#sect_6.7.1.1.1). The following encodings for a tag are supported:

Value|Example
----------|----------
{group}{element}|0020000D
{dicomKeyword}|StudyInstanceUID

Example query searching for instances: **../instances?modality=CT&00280011=512&includefield=00280010&limit=5&offset=0**

### Unsupported Query Paramters
The following parameters noted in the DICOM web standard are not currently supported:

Key|Value|Description
----------|----------|----------
`fuzzymatching=`|true or false|Whether query should use fuzzy matching on the provided {attributeID}/{value} pairs.

Querying using the `TimezoneOffsetFromUTC` (`00080201`) is also not supported.

### Search Response

The response will be an array of DICOM datasets. Depending on the search type, the below attributes will be returned, based on the [IHE standard](https://www.ihe.net/uploadedFiles/Documents/Radiology/IHE_RAD_TF_Vol2.pdf).

When an include field is requested in the query (for study and series searches), it must be one of the below attributes, or it will be ignored in the query. If `includefield=all` is provided all of the mentioned attributes will be returned.

#### Study Search
*Required Attributes:* 
Attribute Name|Tag
----------|----------
Specific Character Set|(0008, 0005)
Study Date|(0008, 0020)
Study Time|(0008, 0030)
Accession Number|(0008, 0050)
Patient Name|(0010, 0010)
Patient ID|(0010, 0020)
Study ID|(0020, 0010)
Study Instance UID|(0020, 000D)
Modalities In Study|(0008, 0061)
Referring Physician Name|(0009, 0090)
Patient Birth Date|(0010, 0030)
Patient Sex|(0010, 0040)
Number Of Study Related Series|(0020, 1206)
Number Of Study Related Instances|(0020, 1208)
Timezone Offset From UTC|(0008, 0201)
Retrieve URL|(0008, 1190)
Instance Availability|(0008, 0056)

*Optional Attributes:*
Attribute Name|Tag
----------|----------
Person Identification Code Sequence|(0040, 1101)
Person Address|(0040, 1102)
Person Telephone Numbers|(0040, 1103)
Person Telecom Information|(0040, 1104)
Institution Name|(0008, 0080)
Institution Address|(0008, 0081)
Institution Code Sequence|(0008, 0082)
Referring Physician Identification Sequence|(0008, 0096)
Consulting Physician Name|(0008, 009C)
Consulting Physician Identification Sequence|(0008, 009D)
Issuer Of Accession Number Sequence|(0008, 0051)
Local Namespace Entity ID|(0040, 0031)
Universal Entity ID|(0040, 0032)
Universal Entity ID Type|(0040, 0033)
Study Description|(0008, 1030)
Physicians Of Record|(0008, 1048)
Physicians Of Record Identification Sequence|(0008, 1049)
Name Of Physicians Reading Study|(0008, 1060)
Physicians Reading StudyIdentification Sequence|(0008, 1062)
Requesting Service Code Sequence|(0032, 1034)
Referenced Study Sequence|(0008, 1110)
Procedure Code Sequence|(0008, 1032)
Reason For Performed Procedure Code Sequence|(0040, 1012)

#### Series Search:
*Required Attributes:*
Attribute Name|Tag
----------|----------
Study Instance UID|(0020, 000D)
Modality|(0008, 0060)
Series Number|(0020, 0011)
Series Instance UID|(0020, 000E)
Number Of Series Related Instances|(0020, 1209)
Series Description|(0008, 103E) 
Requested Procedure ID|(0040, 1001)
Scheduled Procedure Step ID|(0040, 0009)
Performed Procedure Step Start Date|(0040, 0244)
Performed Procedure Step Start Time|(0040, 0245)
Body Part Examined|(0018, 0015)
Specific Character Set|(0008, 0005)
Timezone Offset From UTC|(0008, 0201)
Retrieve URL|(0008, 1190)

*Optional Attributes:*
Attribute Name|Tag
----------|----------
Laterality|(0020, 0060)
SeriesDate|(0008, 0021)
SeriesTime|(0008, 0031)
Performed Procedure Step ID|(0040, 0253)
Referenced SOP Class UID|(0008, 1155)
Referenced SOP Instance UID|(0008, 1155)

#### Instance Search

*Required Attributes:*
Attribute Name|Tag
----------|----------
Study Instance UID|(0020, 000D)
Series Instance UID|(0020, 000E)
Instance Number|(0020, 0013)
SOP Instance UID|(0008, 0018)
SOP Class UID|(0008, 0016)
Rows|(0028, 0010)
Columns|(0028, 0011)
Bits Allocated|(0028, 0100)
Number Of Frames|(0028, 0008)
Specific Character Set|(0008, 0005)
Timezone Offset From UTC|(0008, 0201)
Retrieve URL|(0008, 1190)

*Optional Attributes:*

All attributes available in the DICOM instance except those values not indexed.

### Response Codes

The query API will return one of the following status codes in the response:

Code|Name|Description
----------|----------|----------
*Success*|
200|OK|Whether query should use fuzzy matching on the provided {attributeID}/{value} pairs.
204|No Content|The search completed successfully but returned no results.
*Failure*|
400|Bad Request|The QIDO-RS Provider was unable to perform the query because the Service Provider cannot understand the query component.
401|Unauthorized|The QIDO-RS Provider refused to perform the query because the client is not authenticated.
403|Forbidden|The QIDO-RS provider understood the request, but is refusing to perform the query (e.g. an authenticated user with insufficient privileges).
503|Busy|Service is unavailable

The query API will not return 413 (request entity too large). If the requested query response limit is outside of the acceptable range, a bad request will be returned. Anything requested within the acceptable range, will be resolved.

### Warning Codes

When the API returns information related to the query response, the HTTP response **Warning Header** will be populated with a list of codes and a description. All known warning codes are provided below:

Code|Description
----------|----------
299 {+Service}: There are additional results that can be requested.|The provided query resulted in more results, but has been limited based on the query limits or internal default limits.
299 {+Service}: The fuzzy matching parameter is not supported. Only literal matching has been performed.|Making a request, passing the parameter ?fuzzymatching={value}, will cause this header to be returned.
299 {+Service}: The results of this query have been coalesced because the underlying data has inconsistencies across the queried instances.|The executed query return results that had inconsistent tags at the instance level. A decision has been taken by the server how to merge the inconsistent tags, but this might not be expected by the caller.

### Inconsistent DICOM Tags

It is possible when searching for a study or series, the DICOM tags are inconsistent between the individual instances. The Azure for Health API will allow searching on all inconsistent tags, and aim to provide a consistent behaviour for each search response.

As an example, different instances in the same study could have been created with inconsistent study dates. When this happens, the API will allow searching on inconsistent tags, and return the tag that best matches your query. For an example:

Instance 1
  - Study Instance UID (0020, 000D) = 5
  - Study Date (0008, 0020) = 20190505

Instance 2
  - Study Instance UID (0020, 000D) = 5
  - Study Date (0008, 0020) = 20190510

QIDO Search 1: **../studies?0020000D=5&0020000D=20190510**

Returns:
  - Study Instance UID (0020, 000D) = 5
  - Study Date (0008, 0020) = 20190510

QIDO Search 2: **../studies?0020000D=5&0020000D=20190504-20190507**

Returns:
  - Study Instance UID (0020, 000D) = 5
  - Study Date (0008, 0020) = 20190505

When the query matches both inconsistent tags, one of the matched tags will be consistently chosen; repeated searches will return the same result.

## Delete

The **Azure for Health API** supports the following **HTTP DELETE** endpoints:

Method|Path|Description
----------|----------|----------
DELETE|../studies/{study}|Delete entire study
DELETE|../studies/{study}/series/{series}|Delete entire series
DELETE|../studies/{study}/series/{series}/instances/{instance}|Delete entire instance

Depending on how your server instance has been configured, the API supports the ability to 'soft-delete' (delete index, but not the underlying DICOM file) or 'hard-delete' (delete the index and the underlying DICOM file) DICOM instances depending on your requirements.
