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
- If _dateShiftScope_ is _resource_, dates within the same resource will be assigned the same shifting amount. 
- If _dateShiftScope_ is _file_, dates within the same json/ndjson file will be assigned the same shifting amount.
- If _dateShiftScope_ is _folder_, dates within the whole input folder, namely, all dates in the input, will be assigned the same shifting amount.
- The default _dateShiftScope_ is set to _resource_, which is consistent with the behavior before this parameter is enabled.

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

# Investigation about date shift with compartment scope
A compartment is a logical grouping of resources which share a common property.
The [specification](https://www.hl7.org/fhir/compartmentdefinition.html) defines 5 kinds of compartments: Patient, Encounter, RelatedPerson, Practitioner, Device.

The major problem with this one is that resources may cross between compartments, or interlink them.
Such cross-linking may arise for many valid reasons, including: 
- Cases where subject records are inter-linked - Transplants, etc; 
- Workflow management where action lists link multiple patients and/or practitioners.

It would be difficult for Anonymizer to decide which value to shift for dates in these inter-linked resources.
Instead, we recommend users to save resources that they think should be shifted together to the same file or folder and use _dateShiftScope: file/folder_.