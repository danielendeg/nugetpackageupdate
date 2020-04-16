# Rule configuration improvement for the tool of anonymozation

Based on current design, customer uses Path/Type rule to specify the anonymized properties in the resource. In this document we would discuss about few limitations on current configuration and describe improvement proposal to make the configuration more flexible and extensible to support further more features.

## 1. General FHIR path rule support
Currently we support basic FHIR path without expression and Type rule. Basic path is from resouce type to the detailed node names. And type rules are applied to certain the data types in the resource.

We have following limitations on current Path/Type Rule:

- Cannot select nodes of specified data type like : **Address.country**
- Cannot support nested structure, which usually requires functionality like "wildcard" to support navigation to all descandents. 
- Cannot support condition based paths like: **ContactPoint.where(system=’phone’).value**

### 1.1 Proposed Improvement

FHIR Path is a standard query solution for search element in FHIR resource. It already support condition, expression with some useful functions.
It also covers the Type Rule we currently supported, so we can use FHIR rule as the only one unified rule in config file.

Also we noticed that customer can not easy to use default FHIR path to describe some common cases in our senario like: **"Node1.*.node2"**. In default FHIR path, it should be written as **"(Node1.descendants() | Node1).where(node2)"**.
To make it clear, we plan to extend the symbol of FHIR path, add below 2 useful functions to help customer write clear expressions about wildcard senarios support:

- nodesByType(<type_name>): return nodes list of invokee's descandents with the type name. 
- nodesByName(<name>): return nodes list of invokee's descandents with the node name.
- These two functions will not work to contained resources nested in a resource. For contained resource scenario like Bundle, contained resource. we would discuss in section 1.3.

With the above extended functions, here're some sample FHIR path for common user senarios:

|Path   	                                                |Description   	                                                                    |
|---	                                                    |---	                                                                            |
|Patient.address.state                                      |Basic FHIR path. Same as current path rule.                                        |
|nodesByType(“Age”)                                         |Basic Type rule. Equal to current basic type rule.   	                            |
|nodesByType(“Address”).country   	                        |**Extended Type rule.**  	                                                            |
|RequestGroup.nodesByName(“action”).title   	            |FHIR Path rule apply to all nodes with name “action”. Equal to **Wildcard** “RequestGroup.*.action.title”.   	|
|nodesByType(‘ContactPoint’).where(system=’phone’).value   	|Path sequence with multiple functions, include FHIR build-in function with **Condition**. |

*Implementation details*
- We leverage FHIR Path lib to extend function in FHIR path: [Link](https://github.com/FirelyTeam/fhir-net-common/blob/fe73db2fd9d8b0e8d97a50a60e4e6bdab4257c32/src/Hl7.FhirPath/FhirPath/Expressions/SymbolTable.cs#L128)
- To prevent performance downgrade, we would only apply rules with the same resoruce type (Patient.nodesByType('HumanName')) or general rules without resouce type specified (nodesByType('date')).

### 1.2 Rule Conflicts and Overwriting Strategy
The different FHIR paths might navigate to the same node in the resource and expression make it even more complex.  

To provide a clear stragety to customer. We proposed to apply rule by its order in the config. 

### 1.3 Whitelist
To provide whitelist functionality, we suggest customer use rule with method "keep" to whitelist nodes in the field.
```
{"path": "Patient.nodesByType('Address').state", "method": "keep}
```

Given 1.2 & 1.3 strategy, we would suggest the FHIR Path rules follow below 3 best practices:

- Whitelist path with "keep" method should at first.
- FHIR path rule with resource type should before FHIR path without resource type. Patient.name should before Patient.
- For same resource type, specific rule should before general rule. Patient.address.state should before Patient.address

### 1.4 Bundle & Contained support
FHIR path rule would be applied to all nodes which are FHIR resource (some of them might be included in contained & bundle). 
Bundle & Contained resource might contains different type resources, so one rule might be applied multiple times.

For the extension functions "nodesByType" & "nodesByName", to avoid confliction the results would not contain the nodes in contained nodes and bundle entry nodes.

### 1.5 Extension support
Currently we mark the extension field "redact" in the rule list, but it still might be overwritten by rules before. 
To make the extension support simplifier we suggest to simply remove extension nodes, add below rule at first of the config:

```
{"path": "nodesByType('Extension')", "method": "redact"}
```

### 1.6 FHIR Path validatation
FHIR path contains expressions and can be only evaluated at runtime. We would provide grammar check on the path and provide tools like https://hl7.github.io/fhirpath.js/ to help customer validate their FHIR paths. 

Also we might provide some guidance to customer for how to write their own path. 

## 2. Config file structure improvement
We plan to support more flexible config stucture to further extended features. 
Currently for each rule, we can only specify the method, but under some circumstances, we might need add some rule specific options at rule level like encryption type, postcode options..

To make it more generic we can change the config to below, than we can simple to support rule level options.

```json
{
    "fhirPathRules": [
        { "Path": "Patient.address.state", "method": "redact", "any further options if needed" : "" },
        ...
    ]
}
```

## 3. Backward compatibility
To keep backward compatibility for previous config files. We plan to move forward with following items:
- Add a new section in config file: “fhirPathRules”. Separate new rules with previous one. 
- Keep current logic as V1 and only take effect if “pathRule” & “typeRule” exist in config file.
- Add new logic as V2 for “fhirPathRules” take effect if only “fhirPathRules” exists in config.
- Throw exception if both of V1 & V2 config rules exist in config.
- Document help customer move forward from current config file. 

With above limitation, we can support V1 & V2 config side by side, and only not support V1 & V2 config at the same time.

## 4. Sample config file
```
"fhirPathRules" = [
	{"path": "nodesByType('Extension')", "method": "redact"}

	// Whitelist Rules
	{"path": "Patient.nodesByType('HumanName').use", "method": "keep"},
	{"path": "Patient.nodesByType('Address').state", "method": "keep},
	{"path": "Patient.nodesByType('Address').country", "method": "keep"},

	// Specific Path Rules
	{"path": "Patient.generalPractitioner", "method": "redact"},
	{"path": "Patient.link.other", "method": "redact"},
    
	...............

	{"path": "nodesByType(‘ContactPoint’).where(system=’phone’).value", "method": "redact"}

	// General Type Rules
	{"path": "nodesByType('date')", "method": "dateShift"},
	{"path": "nodesByType('dateTime')", "method": "dateShift"},
	{"path": "nodesByType('instant')", "method": "dateShift"},
	{"path": "nodesByType('Identifier')", "method": "redact"},
	{"path": "nodesByType('HumanName')", "method": "redact"},
	{"path": "nodesByType('Address')", "method": "redact"},
	{"path": "nodesByType('Annotation')", "method": "redact"},
	{"path": "nodesByType('Attachment')", "method": "redact"},
	{"path": "nodesByType('ContactPoint')", "method": "redact"},
	{"path": "nodesByType('Narrative')", "method": "redact"},
]
```
## Open Questions:
1. Should we support yaml format config?
2. List not supported functions in path lib.
