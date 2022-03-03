:::  mermaid
flowchart LR

prClient[Private Client] --> |Customer Content, EUII, EUPI, OII| fesvc
puClient[Public Client] --> |Customer Content, EUII, EUPI, OII| azdns[Azure DNS]
azdns --> |Customer Content, EUII, EUPI, OII|trafficmanager[Azure Traffic Manager]
trafficmanager --> |Customer Content, EUII, EUPI, OII|nginix
fesvc[Frontend Svc] <-->|Customer Content, EUII, EUPI, OII| trafficmanager
aad[Azure AD] --> |OII| dicomsvc
aad <--> |OII|aadpodid

subgraph aks[Azure Kubernetes Service]
nginix[Nginix] --> |Customer Content, EUII, EUPI, OII|dicomsvc
dicomsvc <--> |OII|aadpodid[AAD Pod Identity]
azsecpack[Azure security pack] --> |System Metadata|genevag[Geneva Agent]
dicomsvc -->|OII, System Metadata| genevag
akslogs[AKS Logs]
end

subgraph dicomaz[Dicom Azure resource]
dicomsvc[DICOM Service] -->|Customer Content, EUII, EUPI, OII| azblobstorage[Azure Storage Account: Blob, Table and Queue]
keyvalut[Key Vault]
trafficmanager[Azure Traffic Manager]
end

subgraph sf[Service Fabric]
fesvc <-->|OII, System Metdata| rp[Resource Provider]
end

subgraph aksinfra[AKS Infrastructure Resource]
keyvault2[Key Vault]
acr[ACR]
end

genevag --> |EUII, EUPI, OII, System Metadata| geneva
genevag --> |System Metadata|keyvault2
geneva[Geneva] -->|OII, System Metadata| kusto[Kusto]
geneva --> |EUII, EUPI, OII|azdiag[Azure Diagnostics]
geneva --> |OII, System Metadata|icm[ICM]
kusto -->|OII, System Metadata| bisql[Azure SQL DB - BI]

dicomsvc <-->|Customer Content, EUII, EUPI, OII| dicomdbsql[Azure SQL DB - DICOM]
acr --> |System Metadata|dicomsvc
azblobstorage --> |OII, System Metadata| loganalytics[Azure Log analytics]
keyvalut[Key Vault] --> |OII, System Metadata| loganalytics
azstg --> |OII, System Metadata| loganalytics
keyvault2  --> |OII, System Metadata| loganalytics
nginix --> |System Metadata|keyvault2
dicomsvc --> |System Metadata|keyvault2
dicomsvc --> |System Metadata|keyvalut
akslogs --> |OII, System Metadata| azmonitor[Azure Monitor]
azmonitor --> |OII, System Metadata|icm
rp --> |OII, System Metadata| dicomsvc
:::