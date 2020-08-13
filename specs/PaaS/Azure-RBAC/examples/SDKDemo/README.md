# Simple AzureRBAC demo tool

This tool takes a user/principal object id, a resource, and an action and checks if they have access. To perform the check, we need a service principal (client id and client secret) with rights to read groups from Microsoft Graph and rights to read roles and assignments.

Run the tool with:

```console
.\AzureRBACSDK.exe --client-id xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx --client-secret "XXXXX" --tenant cloudynerd.onmicrosoft.com --oid xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx --resource "/subscriptions/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx/resourceGroups/mygroup/providers/Microsoft.Storage/storageAccounts/mystorage" --action "Microsoft.Storage/storageAccounts/blobServices/containers/blobs/read"
```

