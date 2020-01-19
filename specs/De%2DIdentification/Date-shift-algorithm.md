[[_TOC_]]

# Description
User can choose to de-identify dates and dateTimes by shifting them within a preset range.

# Algorithm details
## Input
- Required: an original date or dateTime object, the resource ID
- Optional: an encrypted base-64 encoded key

## Output
- A shifted date or dateTime object

## Pipeline
1. If a user does not provide a key, a random string will be generated as the key.
2. Combine the key and resource ID into a new string. Convert the above string to byte array.
3. Use BKDR as the hash function to get an integer between [-RANGE, RANGE]
4. Generate the shifted date or dateTime object with the offset

# Exceptions

1. If the date or dateTime object does not contain exact day, like "yyyy", "yyyy-MM", there's no date that can be shifted and default redaction will be applied.
2. If the age implied from date or dateTime object is over 89, all information of the object including year, month and date will be redacted according to HIPAA's requirements.
