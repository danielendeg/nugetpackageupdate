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

**About Hash Collision** A SHA-256 algorithm outputs 64 characters which can either be a lowercase letter or a number from 0-9. The space is quite large while our Ids are of very limited size comparing to SHA-256 output space. Also, SHA-256 collision examples have not been found for years. Even if we happened to encounter a collision example, I think it's acceptable as the probability is quite lower comparing to errors occur in data entry, i.e. a typo. 

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