# Problem
Some editor's JSON validation does not support comments, which will make the user experience worse. For example:
- VS Code
![image.png](/.attachments/image-8deaee64-a5d6-474f-8803-afd2bbe3b0f0.png)
- Notepad++
![image.png](/.attachments/image-ee485762-6b09-4706-946f-25adfa2b56b9.png)

# Options

|  | JSON | YAML | XML |
|--|--|--|--|
| Pros | - Easy  to read and edit; <br><br> - Various data formats are supported (object, array, scalars). | -Support comments; <br><br> - Easy to read and edit; <br><br> - Various data formats are supported (object, array, scalars).| - Support comments |
| Cons | - Comments are not supported <br> <br>- The format is strict (Missing quotes, commas and other symbols can lead to errors). | -The format is strict ( Use indents to represent hierarchy) | - Redundancy; <br><br> - Difficult to read and edit when there are many nesting or hierarchies|
# Sample
- JSON
```Json
{
    "fhirPathRules": [
      {
        "path": "Patient.nodesByType('HumanName')",
        "method": "redact",
        "document/remark": "Comment 0"
      },
      {
        "path": "TestResource",
        "method": "redact",
        "_comment": "Comment 1"
      },
      {
        "path": "nodesByType('HumanName')",
        "method": "redact",
        "document/remark0": "block comment 0",
        "document/remark1": "block comment 1",
        "document/remark2": "block comment 2"
      },
      {
        "path": "Resource",
        "method": "keep"
      }
    ],
    "parameters": {
      "dateShiftKey": "",
      "enablePartialAgesForRedact": true,
      "enablePartialDatesForRedact": true,
      "enablePartialZipCodesForRedact": true,
      "restrictedZipCodeTabulationAreas": [
        "036",
        "059",
        "102",
        "203",
        "205",
        "369",
        "556",
        "692",
        "821",
        "823",
        "878",
        "879",
        "884",
        "893"
      ]
    }
  }
```
- YAML
```yaml
fhirPathRules: 
  # block comment 0
  # block comment 1
  # block comment 2
  - path: "Patient.nodesByType('HumanName')"
    method: "redact"
    # comment 0
  - path: "TestResource"
    method: "redact"
  - path: "nodesByType('HumanName')"
    method: "redact"
  - path: "Resource"
    method: "keep"
parameters: 
  dateShiftKey: ""
  enablePartialAgesForRedact: "true"
  enablePartialDatesForRedact: "true"
  enablePartialZipCodesForRedact: "true"
  restrictedZipCodeTabulationAreas: 
  - "036"
  - "059"
  - "102"
  - "203"
  - "205"
  - "369"
  - "556"
  - "692"
  - "821"
  - "823"
  - "878"
  - "879"
  - "884"
  - "893"
```
- XML
```xml
<?xml version="1.0" encoding="UTF-8" ?>
    <!--  
        block comment 0
        block comment 1
        block comment 2
    -->
    <fhirPathRules>
        <!--  comment 0   -->
	<path>Patient.nodesByType('HumanName')</path> 
	<method>redact</method>
    </fhirPathRules>
    <fhirPathRules>
	<path>TestResource</path>
	<method>redact</method>
    </fhirPathRules>
    <fhirPathRules>
	<path>nodesByType('HumanName')</path>
	<method>redact</method>
    </fhirPathRules>
    <fhirPathRules>
	<path>Resource</path>
	<method>keep</method>
    </fhirPathRules>
    <parameters>
	<dateShiftKey></dateShiftKey>
        <enablePartialAgesForRedact>true</enablePartialAgesForRedact>
	<enablePartialDatesForRedact>true</enablePartialDatesForRedact>
	<enablePartialZipCodesForRedact>true</enablePartialZipCodesForRedact>
        <restrictedZipCodeTabulationAreas>036</restrictedZipCodeTabulationAreas>
	<restrictedZipCodeTabulationAreas>059</restrictedZipCodeTabulationAreas>
        <restrictedZipCodeTabulationAreas>102</restrictedZipCodeTabulationAreas>
	<restrictedZipCodeTabulationAreas>203</restrictedZipCodeTabulationAreas>
	<restrictedZipCodeTabulationAreas>205</restrictedZipCodeTabulationAreas>
	<restrictedZipCodeTabulationAreas>369</restrictedZipCodeTabulationAreas>
	<restrictedZipCodeTabulationAreas>556</restrictedZipCodeTabulationAreas>
	<restrictedZipCodeTabulationAreas>692</restrictedZipCodeTabulationAreas>
	<restrictedZipCodeTabulationAreas>821</restrictedZipCodeTabulationAreas>
	<restrictedZipCodeTabulationAreas>823</restrictedZipCodeTabulationAreas>		<restrictedZipCodeTabulationAreas>878</restrictedZipCodeTabulationAreas>
	<restrictedZipCodeTabulationAreas>879</restrictedZipCodeTabulationAreas>
	<restrictedZipCodeTabulationAreas>884</restrictedZipCodeTabulationAreas>		<restrictedZipCodeTabulationAreas>893</restrictedZipCodeTabulationAreas>
    </parameters>
	

```

