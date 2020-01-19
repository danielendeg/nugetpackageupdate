[[_TOC_]]

# Description
User can choose to de-identify dates and dateTimes by shifting them within a preset range.

# Algorithm details
Input
```
- Required: an original date or dateTime object, the resource ID
- Optional: an encrypted base64-encoded key
```

Output
```
- A shifted date or dateTime object
```

Pipeline
```
- If a user does not provide a key, a random string will be generated as the key.
- Combine the key and resource ID into a new string.
- Convert the above string to byte array.
- Use BKDR as the hash function to get an integer between [-RANGE, RANGE]
- Generate the shifted date or dateTime object with the offset
```

# Exceptions

- If the date or dateTime object does not contain exact day, like "yyyy", "yyyy-MM", there's no date that can be shifted and default redaction will be applied.
- If the age implied from date or dateTime object is over 89, all information of the object including year, month and date will be redacted according to HIPAA's requirements.

# Q&A

1. Why resource ID is used in the date shift algorithm:
If we generate the amount by which dates are shifted only by the key, every date value in the dataset will be shifted with the same offset. However, if resource ID is involved, the offset will be different among different resources, bringing in more randomness. Besides, dates within the same resource will have the same offset, which helps avoid the conflict between dates. For example, if the offset is different within the same resource, the start value may be later than the end value of Period instance.

2. Why use BKDR as the hash function:
This hash function comes from Brian Kernighan and Dennis Ritchie's book "The C Programming Language". It is a simple hash function using a strange set of possible seeds which all constitute a pattern of 31....31...31 etc. The implementation is very concise and meets our needs.
