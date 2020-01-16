# Problem Definition
Whether some FHIR paths are sensitive or not can depends on its parents or siblings. 
It’s not sure how to handle these cases in current config file design. 
For example, shown in following figure, “member” of Resource Group can refer to Patient or Device. 
If it refers to Patient in “entity”, “entity”’s sibling path “period” should be de-identified. 
But when it refers to Device, “period” should not be de-identified. 
# FHIR Path based Solution
In our current design, we utilize FHIR Path to location elements in resources to perform de-identification actions. 
The first fundamental operation is to select a set of elements by their path:
```
path.subPath.subPath - select all the elements on the path
``` 
e.g. To select all the phone numbers for a patient

```
patient.name
```

In addition to selecting subelements, functions can be performed on the FHIR Paths.
As an example:
```
telecom.where(use = 'home').value
```
The function feature can help us find elements with conditions on their parents or siblings. Here we conduct some investigation against two resources: **Group**.

# Investigation on Group members
Here is the schema of [Group resources](https://www.hl7.org/fhir/group.html) with members:
```
{
  "resourceType" : "Group",
  ...,
  "member": [{
      "entity": {Reference(Patient|Practitioner|PractitionerRole|Device|Medication|Substance|Group)},
      "period": { Period },
      "inactive": bool
  }]
}
```
and the schema of a [reference object](https://www.hl7.org/fhir/references.html#Reference):
```
{
    "reference": "<string>",    // Literal reference, Relative, internal or absolute URL
    "type": "<uri>,             // Type the reference refers to (e.g. "Patient")
    "identifier": {Identifier}, // Logical reference
    "display" : "<string>"     // Text alternative for the resource, this text maybe sensitive
}
```
Our current design is a general FHIR path to redact all Period regardless of the type of Reference object. 
```
Group.member.period
```

When a member entity is a type of Patient, Practitioner or PractitionerRole, we may need to redact the period field. But when it's not among these sensitive types, we might not redact the period field.

In this case, we can modify the FHIR Path of Period that need to be redacted to below:
```
Group.member.where(entity.type = 'Patient' or entity.type = 'Practitioner' or entity.type = 'PractitionerRole' or entity.reference.contains('Patient') or entity.reference.contains('Practitioner')).period
```
We write a few conditions here because there are many forms to describe Reference in FHIR and the type field might be missing in [some cases](https://www.hl7.org/fhir/group-example-member.json.html)
We can split the expression above into several short expressions as it is very complicated.
```
Group.member.where(entity.type = 'Patient').period
Group.member.where(entity.type = 'Practitioner').period
Group.member.where(entity.type = 'PractitionerRole').period
Group.member.where(entity.reference.contains('Patient')).period
Group.member.where(entity.reference.contains('Practitioner')).period
```
The functions adds more capabilities to FHIR Path and are helpful to solve the sibling dependencies between elements. But it also makes the config file more complicated.


