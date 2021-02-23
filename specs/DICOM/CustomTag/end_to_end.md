## End to End Scenario
  - [Add custom tag](#add-custom-tag)
  - [Remove custom tag](#remove-custom-tag)
  - [Store instance](#store-instance)
  - [Delete Instance(s)](#delete-instances)

### Add custom tag

1. User can add a new custom tag. Tag information including tagid and job URL is returned if succeed ([Add Custom Tag](#_Add_Custom_Tag) ) '

   ·     Request: 

   ​	POST *//dicomserver/tags*  

   ​    Body:  

   ```json
    [
       {
           "path": "00080107", // path to tag, 
           "vr": "DT", // can be omitted for standard tags
           "level": 1 // 1 – instance level, 2 – series level, 3 – study level
       },
       {
           "path": "00080050",
           "vr": "SH",
           "level": 3
       }
   ]
   ```

   ·     Response: 

   On success will return 202 Accepted with response pay load of

   ```json
   {
       "tags": [
           {
               "tagid": 3,
               "path": "00080107", // path to tag
               "vr": "DT", // can be omitted for standard tags
               "level": 1 // 1 – instance level, 2 – series level, 3 – study level
           },
           {
               "tagid": 4,
               "path": "00080050",
               "vr": "SH",
               "level": 3
           }
       ],
       "job": "//dicomserver/jobs/4" // the url to created jobs
   }
   ```

   ·     Status: 

   o  Reindexing: DICOM is reindexing this tag

   o  Added: The tag has been added and reindexed to DICOM system

   o  Deindexing: DICOM is deindexing this tag

 

2. While user waits for tag reindexing completes: 

   * User can get status through getting tag info by id ([Get Custom Tag](#_Get_Custom_Tag), [Get Job](#_Get_Job))

   * QIDO including this tag is denied

   * User can not add/delete another tag for V1

   * User cannot delete reindexing custom tag until reindexing completes

   * Get Tag API

     * Request: GET //dicomserver/tags/<tagid>

     * Response: On succeeds  return 200 OK with response pay load of 

       ```json
       {
           "Tagid": 3,
           "Path": "00080107",
           "VR": "DT",
           "Level": 1,
           "Status": "Reindexing",
           "Job": //dicomserver/job/4"
       }
       ```

       

       

   * Get Tag API

     * Request: GET //dicomserver/jobs/<jobid>

     * Response: On succeeds return 200 OK with response pay load of 

       ```json
       {
           "JobId": 4,
           "TagId": 3,
           "JobType": "Reindex",
           "CompletedWatermark": null,
           "BoarderWatermark": 1023,
           "EndTimeStamp": "2012-04-23T17: 45: 00.000Z",
           "HeartBeatTimeStamp": "2012-04-23T18:00:00.000Z",
           "Status": "Queued"
       }
       ```

        *JobType*: 

       * *Reindex: reindex this tag* 
       *  *Deindex: deindex this tag* 

         Status:

       * *Queued:* *the  job has been added to Job store but have not been picked up by job executor  (worker) yet*  
       * *Executing: the job has been picked up by worker and  executing now.*  
       * *Error: Fails to execute the job.*

 

3. After reindexing complete, user can query this tag

 

### Remove custom tag

1. User remove an existing custom tag by id. Tag information including tagid and job URL is returned if succeed.([Remove Custom Tag](#_Remove_Custom_Tag))

   ·    Request:  DELETE //dicomserver/tags/<tagid>

   ·    Response: On success will return 202 Accepted with response  pay load of

   ```json
   {
       "Tagid": 3,
       "Status": "Deindexing",
       "Job": "//dicomserver/jobs/4" // the url to created job
   }
   ```

   

2. While user waits for tag reindexing completes:
   - User can get status through getting tag info by id ([Get Custom Tag](#_Get_Custom_Tag), [Get Job](#_Get_Job))
   - QIDO including this tag is denied
   - User can not add/delete another tag for V1
   - User cannot add deindexing custom tag until deindexing completes

3. After remove completes, query on this tag is denied

### Store instance

1. When user store an instance, it is indexed on all existing custom tags, including reindexing ones. ( [Store Dicom Instance](#_Store_Dicom_Instance) )

2. All tags are assumed to be unique within an instance – i.e. if multiple values for the same tag exist within a given instance, an error will be thrown.

3. If multiple values are given for study/series level tags while uploading instances, the last value wins.

4. If multiple values are given for instance level tags while uploading instance, should error out.

 

### Delete Instance(s)

1. When user delete an instance, all custom tag index associated is removed. ([Delete Dicom Instance](#_Delete_Dicom_Instance))