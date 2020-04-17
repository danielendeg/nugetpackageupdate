# Business Justification
Here we propose a mapping table with Id hashing approach to transform resource Ids in FHIR resources.
With this approach, we can achieve the following goals:
1. All resource Ids has been replaced with a Hash value in anonymized output.
2. All literal references has been anonymized by replacing the corresponding resource Id, where we can keep the reference accross difference resources.
3. Customers can do re-identification with anonymized result. (PaaS version only. TODO in the future)

# Replacing resource Ids

# Anonymize