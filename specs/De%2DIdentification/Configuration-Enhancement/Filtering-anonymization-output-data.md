[[_TOC_]]

# Business Justification
Currently, we copy the entire FHIR data to the output after anonymizing as per the configuration file. While this may be ok in some scenarios, it has the risk of leaking unintended information in the output. For example, the data steward may not be aware that the FHIR resource also contains a text field that can potentially have identifying information.

Another approach is to be explicit about the fields to be copied to the output. This approach significantly reduces the risk of unintended information leak.

# Design
With our current config design, user can specify additional "filtering" rules to restrict fields in anonymization output. 

Here is a sample of anonymization configuration rules
```json
    {
        "Patient.address.state": "keep",
        "Patient.address.country": "keep",
        "Patient.contact.address.state": "keep",
        "Patient.contact.address.country": "keep",
        "Patient.generalPractitioner": "redact",
        "Patient.link.other": "redact"
    }
```
Users can append filtering rules to the anonymization rules to keep fields in output, like "Patient.contact":"keep", this rule keep all data under "Patient.contact" after anonymization process,
Then in the end, users can add a super redaction rule "Resource":"redact", which removes all fields that not covered by "keep" rules. 
Here is a filtering example
```json

    {
        // anonymization rules
        "nodesByType('date')": "dateshift",
        "Patient.nodesByType('Address').state": "keep",
        "Patient.nodesByType('Address').country": "keep",
        "Patient.nodesByType('Address')": "redact",
        "Patient.nodesByType('HumanName').use": "keep",
        "Patient.nodesByType('HumanName')": "redact",
        "Patient.generalPractitioner": "redact",
        "Patient.link.other": "redact",

        // filtering rules
        "Patient.contact": "keep",
        "Patient.communication": "keep",
        // super redaction rule
        "Resource":"redact"
    }
```

As our rules come with a line-sequence priority, anonymization rules will first be applied, and then the "keeping" rules, finally the super redaction rule takes effect, it will remove all nodes without "keep" rules. 

> Specifically, there would be some corner cases for "dateShift" / "partial redact" which will keep an anonymized date/postalcode node to output because super redaction rule has lower priority than dateshift rules. For example, "Patient.birthDate" will get dateshifted by the "date" type rule, it will not get redacted by "Resource":"redact". 

> From my point of view, this could be an acceptable result as all the exceptions are anonymized nodes (dateshift /partial redact) and most identifiers have been covered. If a customer need to rule out some anonymized nodes/values, he also have an option to add specific blacklist rule ahead like "Patient.birthDate":"redact".