# Problem
Now, some editors' JSON validation still don't support comments, which will make the user experience worse. For example:
- VS Code
![image.png](/.attachments/image-464ae237-0407-4fc6-a171-4962385beb6e.png)
- Notepad++
![image.png](/.attachments/image-42ba9f98-1c2f-4a7e-974a-e873a820b9e1.png)

However, adding comments to JSON files in some editors is supported like Visual Studio and Sublime Text. 
- Visual Studio
![image.png](/.attachments/image-892d2d89-b9ea-425f-a498-947e92e4f473.png)

- Sublime Text
![image.png](/.attachments/image-dae7ef0c-7aae-4a04-b0ae-b800c95bd69e.png)

# Comparison

|  | <center>JSON</center> | <center>YAML</center> | <center>XML</center> |
|--|--|--|--|
| Pros | - Easy  to read and edit; <br> - Various data formats are supported (object, array, scalars). | - Support comments; <br> - Easy to read and edit; <br> - Various data formats are supported (object, array, scalars).| - Support comments. |
| Cons | - Comments are not supported; <br> - The format is strict (Missing quotes, commas and other symbols will lead to errors). | - The format is strict ( Use indents to represent hierarchy, so indent mistakes will lead to errors). | - Redundancy; <br> - Difficult to read and edit when there are many nesting or hierarchies.|
# Sample
- JSON
 ( Comments can be written in key-value format )
```Json
{
  "fhirPathRules": [
    {
      "path": "Patient.nodesByType('HumanName')",
      "method": "redact",
      "document/remark": "Comment 0"
    },
    {
      "path": "TestResource",
      "method": "redact",
      "_comment": "Comment 1"
    },
    {
      "path": "nodesByType('HumanName')",
      "method": "redact",
      "document/remark": "block comment 0",
      "document/remark": "block comment 1",
      "document/remark": "block comment 2"
    },
    {
      "path": "Resource",
      "method": "keep"
    }
  ],
  "parameters": {
    "dateShiftKey": "",
    "enablePartialAgesForRedact": true,
    "enablePartialDatesForRedact": true,
    "enablePartialZipCodesForRedact": true,
    "restrictedZipCodeTabulationAreas": [
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
- YAML ( Comments can be written after `#` )
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
- XML ( Comments can be written like `<!--  comment -->` )
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
    <restrictedZipCodeTabulationAreas>823</restrictedZipCodeTabulationAreas>
    <restrictedZipCodeTabulationAreas>878</restrictedZipCodeTabulationAreas>
    <restrictedZipCodeTabulationAreas>879</restrictedZipCodeTabulationAreas>
    <restrictedZipCodeTabulationAreas>884</restrictedZipCodeTabulationAreas>
    <restrictedZipCodeTabulationAreas>893</restrictedZipCodeTabulationAreas>
</parameters>
```

