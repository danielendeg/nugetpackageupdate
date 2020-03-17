# Configuration Enhancement
[[_TOC_]]

# Business Justification
Currently users can use path rules and type rules to cofigure anonymization actions (redact/dateshift) to a FHIR resource.
When composing a sample configuration file on ourselves, we found there are some limitations on our current configuration file design:
* Data type rules are aggressively applied to the entire type. For example, a rule *"HumanName":"redact"* redacts all field in HumanName type. People may want to keep non-sensitive fields like *"HumanName.user"*
* Users have to write redundant path rules to redact complex types like *Reference* where we want to keep all fields except *Reference.display*.
* Users cannot custom anonymization with [nested patterns](https://microsofthealth.visualstudio.com/Health/_workitems/edit/72536/) in FHIR resource. Currently we just remove all nested items aggresively with path rule *QuestionnaireResponse.item.item:redact*.
* Users cannot custom anonymization with [choice elements](https://microsofthealth.visualstudio.com/Health/_workitems/edit/72447/) in FHIR resource. 
* Only basic FHIRPaths in the format of *Patient.name.family* are supported. Users may have advanced demand like writing wildcards rules *name.family* where name.family in every resource can be processed, or writing conditional path rules based on field value.

Here we propose to enhance the anonymization configuration file to address these limitations.

# Design
There are several considerations in designing the enhanced configuration file:
* We want to extend type rules to detailed fields in a data type. Users should be able to specify *Address.country: keep* as well as *Address: redact*.
* We want to support all features of FHIRPath in composing path rules. Note all features means all FHIRPaths supported by [fhir-net-api](https://github.com/FirelyTeam/fhir-net-api) since our codes are depending on the [Hl7.FhirPath](https://www.nuget.org/packages/Hl7.FhirPath/) nugget package.
* The new configuration should be compatiable with the previous one. The previous rules can be correctly interpreted in the new configuration file. 
* The anonymization efficiency with new configuration file should be comparable to the previous one.

The new configuration file has two core features: extended Type rule and generic FHIRPath rule .

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

## Generic FHIRPath Rule
Users can compose all FHIRPaths supported by [fhir-net-api](https://github.com/FirelyTeam/fhir-net-api). 

Here is a sample of generic FHIRPath rules. Users can keep telecom information that is not a phone number and locations that are not no longer active. Users can also redact *"Condition.onset[x]"* field if it is a string type. Anonymization actions for other types of *"Condition.onset[x]"* can be specified in type rules like Age, dateTime.
```json
{
    "pathRules": {
        "name.where(use = 'official')": "keep",
        "telecom.where(system != 'phone')": "keep",
        "Condition.onset as string": "redact",
        "Location.where(status = 'active').name": "redact", 
        "Location.alias": "redact",
        "Location.description": "redact",
        "Location.position": "redact"
    }
}
``` 
## Rule Conflicts and Overwriting Strategy
The most straightforward conflict is that users have given different anonymization actions to a same rule path by mistake, like *"HumanName:keep"* and *"HumanName:redact"*,
this conflict can be detected in the JSON file parsing logic.

However, there are still three kinds of conflicts that may happen in the new configuration file.
1. **Conflicts among FHIRPath rules**, like *"name:keep"* or *"name.(use = "official"):keep"* where different paths can denote to a same node in FHIR structure.
2. **Conflicts among type rules**. like *"HumanName.period.start:keep"* and *"dateTime:redact"* where different type paths can denote to a same node in FHIR structure.
3. **Conflicts between type rule and FHIRPath rule**. like *"Patient.name.period.start:keep"* and *"dateTime:redact"* where different kinds of rule paths can denote to a same node in FHIR structure.

Also, if a node is not specified with any anonymization rule, the rule of parent node will take effect. For example, *"name:redact"* will take effect on it's child nodes like *"name.family"*, *"name.use"*, *"name.given"*, *"name.period"*. This is the same with *"HumanName:redact"* on *"HumanName.family"*, *"HumanName.use"*, *"HumanName.given"*, *"HumanName.period"*. This would make the conflict scenario more complicated. 

Here we define a **Best Practise** to compose configurations:
1. Always write type rules first. Type rules are more flexible and reusable as data types are used accross different resources and different resource structure. For example, *"Address:redact"* is prefered over *"address:redact"* because it will also works to *"contact.address:redact"*. As far as I know, most sensitive data can be captured with Type rules. 
2. FHIRPath rules are more suitable with conditional anonymization and free text of *string* types. Like *"telecom.where(system != 'phone')"* and *"Location.description"*.

So the overwriting strategy to solve conflicts problem based on the Best Practise would be:
1. To solve **conflicts among FHIRPath rules**, we adopt the same philosophy as the previous configuration that deep path overwrites shallow path. Given *"name.family:keep"* and *"name:redact"*, fields of *"name"* will be remdacted except the *"family"* field. <br/> For same-level rules like *"name:keep"* and *"name.(use = "official"):keep"*, which is a new issue that the previous basic FHIRPaths could be in the same level, here we just replace the first one with the latter one in line order in configuration file. 

2. To solve **conflicts among Type rules**, it's fine to continue with the previous setting where type rules *"HumanName:redact"* and *"dateTime:dateshift"* will redact the dateTime in *"HumanName.period.start"* following the logic that high level types overwrite low level types.  <br/> **But from my perspective**, this only applies to coarse-grained Type rules where user cannot customize a child field type of another type, like *"address:keep"* and *"dateTime:dateshift"*, user may want to keep *"address.period.start"* as well, but he cannot accomplish this with type rules.<br/> In fine-grained extended Type rule setting, users can have more capabilities in configuring Type rules. In other word, when a user writes an anonymization rule against a certain type, the expected result should be that the same anonymization action can be applied to all nodes of this type. Take *"HumanName:redact"* and *"dateTime:dateshift"* as an example, all fields will be redacted except *"HumanName.period.start"* and *"HumanName.period.end"*. For *"address:keep"* case, users can specify *"address.period.start:keep"*. <br/>
Like we described in FHIRPath rule conflicts section, same-level conflicts are also possible in type rules like *"HumanName.period.start:keep"*, *"Period.start:redact"* and *"dateTime:redact"*. In this situation, we will pick **the most specific path** which has the highest ancestor of a node: *"HumanName.period.start:keep"*.
If a user do need to customize anonymization like removing some dateTime nodes in *"HumanName"* type, he will be able to specify another rule *"HumanName.period.start"*. 

3. To solve **conflicts between FHIRPath rules and Type rules**, we follow the same philosophy as the previous configuration that path rules overwrite type rules. As we said before, type rules will anonymize the majority of identifiers and FHIRPath rules are more suitable for corner case handling, path rules should be of high prioprity. For example, with path rule (*"Organization.address:keep"*) and type rules (*"Address:redact"*, "dateTime:dateshift"), all fileds including *"Organization.adress.period.start"* in *"Organization.adress"* should be kept.

# Testing
FHIR Anonymization Tool with new configuration file is a significant change to our repo. We need careful testing work including:
1. Unit tests and functional tests.
2. Testing against commandline tool with synthea data.
2. Testing against Azure Data Factory tool with synthea data locally.

Accept criterias:
1. New configuration file should not miss any identifiers comparing to the previous one.
2. New configuration file can handle all FHIRPath rules and extended type rules.
3. Efficiency of new configuration file should be comparable to the previous one.
4. Unit tests and functional tests pass.
5. Commandline and ADF tool works well.
