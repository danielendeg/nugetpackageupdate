# Problem
Some editor's JSON validation does not support comments, which will make the user experience worse. For example:
- VS Code
![image.png](/.attachments/image-8deaee64-a5d6-474f-8803-afd2bbe3b0f0.png)
- Notepad++
![image.png](/.attachments/image-ee485762-6b09-4706-946f-25adfa2b56b9.png)

# Options

|  | JSON | YAML | XML |
|--|--|--|--|
| Pros | - Easy  to read and edit; <br> - Various data formats are supported (object, array, scalars). | -Support comments; <br>- Easy to read and edit; <br> - Various data formats are supported (object, array, scalars).| - Support comments |
| Cons | - Comments are not supported (Although JSON can indirectly add comments through key-value, it will reduces the readability of the configuration file.) - The format is strict (Missing quotes, commas and other symbols can lead to errors.) | -The format is strict (- Use indents to represent hierarchy - Indent does not allow tab, only spaces are allowed) | - Redundancy; - Difficult to read and edit when there are many nesting or hierarchies|
# Sample
- JSON
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
        "document/remark0": "block comment 0",
        "document/remark1": "block comment 1",
        "document/remark2": "block comment 2"
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
![image.png](/.attachments/image-ea96b94f-e1d4-4956-afde-320528ec93a0.png)
- XML
![image.png](/.attachments/image-9171226f-0cf7-4e64-9cdb-ea968b4d3b52.png)

