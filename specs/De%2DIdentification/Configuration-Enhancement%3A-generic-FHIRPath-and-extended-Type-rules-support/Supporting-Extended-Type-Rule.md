# Configuration Enhancement
[[_TOC_]]

# Business Justification
Currently users can use type rules to cofigure anonymization actions (redact/dateshift) to a FHIR resource.
When composing a sample configuration file, we found there are some limitations with the currently supported data types:
* Data type rules are aggressively applied to the entire type. For example, a rule *"HumanName":"redact"* redacts all field in HumanName type. People may want to keep non-sensitive fields like *"HumanName.use"*
* Users have to write redundant path rules to redact complex types like *Reference* where we want to keep all fields except *Reference.display*.
* Users cannot custom anonymization with [nested patterns](https://microsofthealth.visualstudio.com/Health/_workitems/edit/72536/) in FHIR resource. Currently we just remove all nested items aggresively with path rule *QuestionnaireResponse.item.item:redact*.

Here we propose to support extended Type rules in the anonymization configuration file to address these limitations.

# Design
There are several considerations in designing the enhanced configuration file:
* We want to extend type rules to detailed fields in a data type. Users should be able to specify *Address.country: keep* as well as *Address: redact*.
* The new configuration should be compatiable with the previous one. The previous rules can be correctly interpreted in the new configuration file. 
* The anonymization efficiency with new configuration file should be comparable to the previous one.

## Extended Type Rule
There are two kinds of Type rules in new configuration file, the "original Type rule" and "extended Type rule". 

Each rule should begin with a data type identifier (Pascal case) and you can find all data types in this [link](http://hl7.org/fhir/R4/datatypes.html).
Extended type rules have a path suffix denoting a field of the data type. 

Here is a sample of extended type rule. In this sample, we redact fields in *Address* type other than *country* and *state*, *CodeableConcept.text* and *Referrence.display* field.

Also, we fix the [nested item problem](https://microsofthealth.visualstudio.com/Health/_workitems/edit/72536/) by redacting *answer.value*, *text*, *title*, *description*, *textEquivalient* fields in BackBoneElement type without composing infinite nested path rules. 
```json
{
    "typeRules": {
        "base64Binary": "redact",
        "date": "dateShift",
        "dateTime": "dateShift",
        "instant": "dateShift",
        "Address": "redact",
        "Address.coungtry": "keep",
        "Address.state": "keep",
        "Age": "redact",
        "Annotation": "redact",
        "Attachment.title": "redact",
        "Attachment.url": "redact",
        "BackboneElement.answer.value": "redact",
        "BackboneElement.text": "redact",
        "BackboneElement.title": "redact",
        "BackboneElement.description": "redact",
        "BackboneElement.textEquivalent": "redact",
        "CodeableConcept.text": "redact",
        "Coding.display": "redact",
        "Coding.code": "redact",
        "ContactDetail.name": "redact",
        "ContactPoint.value": "redact",
        "Extension": "redact",
        "HumanName": "redact",
        "HumanName.use": "keep",
        "Identifier.value": "redact",
        "Narrative.div": "redact",
        "Reference.display": "redact"
  }
}
```

## Rule Conflicts and Overwriting Strategy
The most straightforward conflict is that users have given different anonymization actions to a same rule path by mistake, like *"HumanName:keep"* and *"HumanName:redact"*,
this conflict can be detected in the JSON file parsing logic.

However, there are still two kinds of conflicts that may happen in the new configuration file.
1. **Conflicts among type rules**. like *"HumanName.period.start:keep"* and *"dateTime:redact"* where different type paths can denote to a same node in FHIR structure.
2. **Conflicts between type rule and FHIRPath rule**. like *"Patient.name.period.start:keep"* and *"dateTime:redact"* where different kinds of rule paths can denote to a same node in FHIR structure.

So the overwriting strategy to solve conflicts problem based on the Best Practise would be:

1. To solve **conflicts among Type rules**, we adopt the same philosophy as the previous configuration that deeper type overwrites shallow type. In other words, rules will be applied to a certain type without overwriting from parent types. Take *"HumanName:redact"* and *"dateTime:dateshift"* as an example, all fields will be redacted except *"HumanName.period.start"* and *"HumanName.period.end"* that are dateshifted.  <br/><br/>
However, like we described in FHIRPath rule conflicts section, same-level conflicts are also possible in type rules like *"HumanName.period.start:keep"*, *"Period.start:redact"* and *"dateTime:redact"* where different rules are describing a same type. In this situation, we will pick **the most specific path** which has the highest ancestor of a node: *"HumanName.period.start:keep"*.
If a user do need to customize anonymization like removing some dateTime nodes in *"HumanName"* type, he will be able to specify another rule *"HumanName.period.start"*. 

2. To solve **conflicts between FHIRPath rules and Type rules**, we follow the same philosophy as the previous configuration that path rules overwrite type rules. As we said before, type rules will anonymize the majority of identifiers and FHIRPath rules are more suitable for corner case handling, path rules should be of high prioprity. For example, with path rule (*"Organization.address:keep"*) and type rules (*"Address:redact"*, "dateTime:dateshift"), all fileds including *"Organization.adress.period.start"* in *"Organization.adress"* should be kept.

# Testing
FHIR Anonymization Tool with new configuration file is a significant change to our repo. We need careful testing work including:
1. Unit tests and functional tests.
2. Testing against commandline tool with synthea data.
2. Testing against Azure Data Factory tool with synthea data locally.

Accept criterias:
1. New configuration file should not miss any identifiers comparing to the previous one.
2. New configuration file can handle all extended Type rules.
3. Efficiency of new configuration file should be comparable to the previous one.
4. Unit tests and functional tests pass.
5. Commandline and ADF tool works well.
