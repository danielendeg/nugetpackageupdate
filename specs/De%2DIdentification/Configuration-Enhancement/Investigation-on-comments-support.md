# Problem
Some editor's JSON validation does not support comments, which will make the user experience worse. For example:
- VS Code
![image.png](/.attachments/image-8deaee64-a5d6-474f-8803-afd2bbe3b0f0.png)
- Notepad++
![image.png](/.attachments/image-ee485762-6b09-4706-946f-25adfa2b56b9.png)

# Options

|  | JSON | YAML | XML |
|--|--|--|--|
| Pros | - Easy  to read and edit; <br> - Various data formats are supported (object, array, scalars). | -Support comments; <br>- Easy to read and edit; <br> - Various data formats are supported (object, array, scalars).| - Support comments |
| Cons | - Comments are not supported (Although JSON can indirectly add comments through key-value, it will reduces the readability of the configuration file.) - The format is strict (Missing quotes, commas and other symbols can lead to errors.) | -The format is strict (- Use indents to represent hierarchy - Indent does not allow tab, only spaces are allowed) | - Redundancy; - Difficult to read and edit when there are many nesting or hierarchies|
# Sample
- JSON
![image.png](/.attachments/image-bffa9d58-1ac1-4575-af5c-5cd031fe1630.png)
- YAML
![image.png](/.attachments/image-3a2cd967-54b9-403f-b482-ba769e3a3393.png)
- XML
![image.png](/.attachments/image-9171226f-0cf7-4e64-9cdb-ea968b4d3b52.png)

