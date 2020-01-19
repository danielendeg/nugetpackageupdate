[[_TOC_]]

# Description
User can choose to de-identify dates and dateTimes by shifting them within a preset range.

# Algorithm description
## Input
- Required: an original date or dateTime object, the resource ID
- Optional: an encrypted base-64 encoded key

## Output
- A shifted date or dateTime object

## Process
1. If a user does not provide a key, a random string will be generated as the key.
2. With the generated key or user-provided key, 
