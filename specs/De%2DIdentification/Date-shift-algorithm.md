[[_TOC_]]

# Introduction
This algorithm provides users with a method to de-identify _date_, _dateTime_ and _instant_ values by shifting them within a preset range.
Currently, the range is set to 100 days ([-50, 50]).

# Configuration settings
Following parameters are applied to date shift algorithm:
```json
{
  "parameters": {
    "dateShiftKey": "123",
    "dateShiftScope": "resource"
}
```

|Method|Parameter|Affected fields|Valid values|Default value|Description
|:-:|:-:|:-:|:-:|:-:|:-:|
|dateShift|dateShiftKey|date, dateTime, instant fields|string|A randomly generated string|This key is used to generate the shifting amount in the date shift algorithm. 
|dateShift|dateShiftScope|date, dateTime, instant fields|resource, file, folder|resource|This parameter is used to select the scope. Data within the same scope will be assigned the same shifting amount. It only works when _dateShiftKey_ is provided.

Details for _dateShiftScope_:
- If _dateShiftScope_ is _resource_, dates within the same resource will be assigned the same shifting amount. [@<7C029C39-BC06-6716-9569-44023A0AA6DA> , how does this scope work for Bundle resource (or any resource with contained resources)? Is date shifting same for all in the Bundle or different for each individual entry resource?  -deepak]
- If _dateShiftScope_ is _file_, dates within the same json/ndjson file will be assigned the same shifting amount.
- If _dateShiftScope_ is _folder_, dates within the whole input folder, namely, all dates in the input, will be assigned the same shifting amount. [ @<7C029C39-BC06-6716-9569-44023A0AA6DA> , does it work recursively in the folder if -r flag is used?]
- The default _dateShiftScope_ is set to _resource_, which is consistent with the behavior before this parameter is enabled.
[ @<7C029C39-BC06-6716-9569-44023A0AA6DA> we intend to integrate anonymizer into the managed services as a special endpoint. that end point will always return anonymized results to the queries. what flag should be used in that case?] [ @<7C029C39-BC06-6716-9569-44023A0AA6DA>, @<356939D1-F4CA-6BA1-875C-7247D42D7353>, Does this slightly different nomenclature (instead of file/folder) makes it more aligned with managed endpoint : individual resource (DomainResource), Bundle, all?] @<7AEC8627-72FE-4CC7-8062-C348124CA707> that makes sense. Note, however, that there is a difference in meaning. A bundle is not same as a file. If I am not mistaken, you can have multiple bundles in one file. @<7C029C39-BC06-6716-9569-44023A0AA6DA> please investigate the impact of this.]

# Implementation

## Input
- [Required] A date/dateTime/instant value.
- [Optional] _dateShiftScope_. If not specified, _resource_ will be set as default scope.
- [Optional] _dateShiftKey_. If not specified, a randomly generated string will be used as default key.

## Output
* A shifted date/datetime/instant value

## Steps
1. Get _dateShiftPrefix_ according to _dateShiftScope_.
- For scope _resource_, _dateShiftPrefix_ refers to the resource id.
- For scope _file_, _dateShiftPrefix_ refers to the file name.
- For scope _folder_, _dateShiftPrefix_ refers to the input folder name.
2. Create a string by combining _dateShiftPrefix_ and _dateShiftKey_.
3. Feed the above string to hash function to get an integer between [-50, 50]. 
4. Use the above integer as the offset to shift the input date/dateTime/instant value.

## Note (unchanged as the one on GitHub)
1. If the input date/dateTime/instant value does not contain an exact day, for example dates with only a year ("yyyy") or only a year and month ("yyyy-MM"), the date cannot be shifted and redaction will be applied.
2. If the input date/dateTime/instant value is indicative of age over 89, it will be redacted (including year) according to HIPAA Safe Harbor Method.
3. If the input dateTime/instant value contains time, time will be redacted. Time zone will keep unchanged.

# Investigation about date shift with compartment (patient) scope
A compartment is a logical grouping of resources which share a common property.
The [specification](https://www.hl7.org/fhir/compartmentdefinition.html) defines 5 kinds of compartments: Patient, Encounter, RelatedPerson, Practitioner, Device.
We can take the patient as the scope for date shift.

The major problem with this one is that resources may cross between compartments, or interlink them.
Such cross-linking may arise for many valid reasons, including: 
- Cases where subject records are inter-linked - Transplants, etc; 
- Workflow management where action lists link multiple patients and/or practitioners.

It would be difficult for Anonymizer to decide which value to shift for dates in these inter-linked resources.

Example 1, Patient _a_ with shifting amount _x_ and Patient _b_ with shifting amount _y_ are mentioned in Group _c_.
For dates in Group _c_, does Anonymizer shift them by amount _x_ or _y_?
More complicatedly, if a Person _d_ is related to both Patient _a_ and _b_, like a mother of twins, for dates in Person _d_, does Anonymizer shift the dates with amount _x_ or _y_?
If we treat Person _d_ as an individual, does Anonymizer shift the dates by an independent amount _z_?

Example 2, in Patient's definition, a Patient _a_ can link to another Patient _b_.
Does Anonymizer shift the dates in all resources related to Patient _a_ and _b_ with same amount?

We need to define the behavior of cases like these before implementation.
Based on the complicated relationship between resources, the implementation could be complicated as well.
Currently, Anonymizer does not understand the concept of compartment.
To enable this, we need to create a new module to figure out the relationship between resources.
For this mid April release, we recommend users to save resources that they think should be shifted together to the same file or folder and use _dateShiftScope: file/folder_.

[ @<7C029C39-BC06-6716-9569-44023A0AA6DA> . Great investigation. Date is a very valuable field for analytics, which is also extremely difficult to anonymize. No wonder safe harbor method asks for removal of dates.

We want to date-shift at a broader scope than resource because date-shifting at the resource level makes the dataset inconsistent and unusable. However, if we date-shift at too broad scope, date-shifting delta can be relatively easily guessed. For example, date of a catastrophic event and related hospitalizations can be used as an anchor to identify the date-shift delta.

The motivation for date shifting at patient compartment scope is to make it more difficult to re-identify the patient. As per one estimate, 50% of the US population can be uniquely identified using zip code, gender, and DOB (https://www.hhs.gov/hipaa/for-professionals/privacy/special-topics/de-identification/index.html). So, protecting DOB is extremely important.  

As you figured out, date-shifting at patient compartment level gives rise to some complexity. My current thought is to dateshift shared resources independently of the patient resource. We will have to come up with a list of such shared resources. In case of linked patients (duplicate record etc.) we will need to make sure that the linked records are shifted by the same value. We will document the behavior and limitations so that the users can make informed decisions. Yes, the implementation is going to be little complicated. We can go with file and folder level dateshifting for now and revisit patient compartment level later.]