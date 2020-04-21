# Business Justification
Here we propose a mapping table with Id hashing approach to transform resource Ids in FHIR resources.
With this approach, we can achieve the following goals:
1. All resource Ids has been replaced with a Hash value in anonymized output.
2. All literal references has been anonymized by replacing the corresponding resource Id, where we can keep the reference accross difference resources.
3. Customers can do re-identification with anonymized result. (PaaS version only. TODO in the future)

# Replacing resource Ids
Each resource has an "id" element which contains the logical identity of the resource assigned by the server responsible for storing it. Logical ids (and therefore literal identities) are case sensitive.

Ids can be up to 64 characters long, and contain any combination of upper and lowercase ASCII letters, numerals, "-" and ".".

Regex: ```[A-Za-z0-9\-\.]{1,64}```

Here we apply a SHA256 hashing algorithm to get an anonymized 256-bits resource Id. Then we can fit the Id format conformance with a Hex representation.
[ @<8ED32720-FC34-6AEA-9795-3EE47CE9512B> we need to use keyed-hashing (HMAC with SHA256?) with user provided key in the config. -deepak]
[ @<7AEC8627-72FE-4CC7-8062-C348124CA707> Agreed. Will change to HMAC with SHA256. -yue]

**About Hash Collision** A SHA-256 algorithm outputs 64 characters which can either be a lowercase letter or a number from 0-9. The space is quite large while our Ids are of very limited size comparing to SHA-256 output space. Also, SHA-256 collision examples have not been found for years. Even if we happened to encounter a collision example, it's acceptable for a small error against a large resource collection. And the probability is quite lower comparing to errors might occur in data entry, i.e. a typo.  

# Replacing literal references
In a resource, references are represented with a reference (literal reference), an identifier (logical reference), and a display (text description of target).
A literal reference is a reference to a location at which the other resource is found. The reference may be a relative reference, in which case it is relative to the service base URL, or an absolute URL that resolves to the location where the resource is found. The reference may be version specific or not. If the reference is not to a FHIR RESTful server, then it should be assumed to be version specific. Internal fragment references (start with '#') refer to contained resources.

Here we will resolve and replace resource Ids from the following literal [reference URLs](https://www.hl7.org/fhir/references.html#literal):
1. An absolute URL consistent with a FHIR API, i.e.
 ```json   
{ "reference" : "http://fhir.hl7.org/svc/StructureDefinition/c8973a22-2b5b-4e76-9c66-00639c99e61b" }
```
2. A relative URL consistent with a FHIR API, i.e
```json
{ "reference" : "Patient/034AB16" }
```
3. Other logical URIs, here we only handle uuid and oid format that are mentioned in Hl7 website as we have not meet other URI formats yet.
```json
{ "reference": "urn:uuid:5f158de76e24b195cb1e4a3e7cb24feb4c4043623bca4e7c03ea07478b19f324" }
```
4. internal reference for contained resource, i.e.
```json
{ "reference" : "#p1" }
``` 
> Specially, for a resource that references the container, the reference is "#", <reference value="#"/>, we will keep the reference unchanged here.

Currently, the above 4 kinds of literal reference covers most patterns in data we have processed (maybe all, need validation from testing result).

# Configuration Setting
Customers can enable/disable resource Id replacement just as anonymizing other identifiers with configuration file v2.
```json
"fhirPathRules" : [
{
    "path": "Resource.id",
    "method": "cryptoHash"
},
{
    "path": "nodesByType('Reference').reference",
    "method": "cryptoHash"
},
"Parameters": {
    "cryptoHashKey":"key_for_HmacSHA-256"
}
```
Here we want to define a new de-identification action **cryptoHash** as this transformation is different from existing operations like redact and dateshift.  
1. For *id* and *string* types, we compute the *HMAC_SHA256* of the id and transform to hex format confirmed to FHIR.
2. For "nodesByType('Reference').reference", only the resource id part will be transformed.
3. For elements with format conformance requirements like date/code/uuid/oid, "cryptoHash" is not supported. [ @<8ED32720-FC34-6AEA-9795-3EE47CE9512B> , a couple of questions. a) How do we enforce this limitation? Do we enforce this while validating the config file? b) above we mention this: "Other logical URIs, here we only handle uuid and oid format that are mentioned in Hl7 website as we have not meet other URI formats yet." That sounds conflicting. Please clarify. ]

[ @<356939D1-F4CA-6BA1-875C-7247D42D7353> Some specifications: \
a. We cannot enforce the limitation during config validation as it's hard to figure out the datatype in a FHIRPath . When we perform cryptoHash operations on a element, we will check if the type of the element is a *FHIR.id* or *FHIR.string* type, if not, the element won't be changed and we can report this behavior in verbose log. \
b. As *CryptoHash* will be applied to only *FHIR.id* and *FHIR.string* elements, other elements like [canonical](https://www.hl7.org/fhir/datatypes.html#canonical)/uri/url/[uuid](https://www.hl7.org/fhir/datatypes.html#uuid)/oid would not be processed because those types has restricted format regex. But *Reference.reference* is a special case here as it's a *FHIR.string* type but its value could be a url/uuid/oid and we only hash the id part in its value.]

As **cryptoHash** changes the resource content, we also need to add a [security tag](https://www.hl7.org/fhir/v3/ObservationValue/cs.html#v3-ObservationValue-PSEUDED) to resources that id has been replaced, possible tags can be **CRYTOHASH** or **PSEUDED**.