# Kubernetes IaC Lab with .NET and SQL Server

**By Professor Daniel Lim-Apo**

Deploy a product catalog API to a local Kubernetes cluster. The application uses C#, ASP.NET Core on .NET 10, Entity Framework Core, and SQL Server. The YAML manifests define the infrastructure as code, from database storage to the API rollout.

[Open the English HTML tutorial](Kubernetes_IaC_DotNet_SQLServer_Tutorial_EN.html). Download the HTML and open it in a browser to use the slide navigation, table of contents, and copy buttons.

## What you will practice

- Build a multi-stage Docker image for an ASP.NET Core API.
- Supply application settings with a ConfigMap and the database password with a Secret.
- Run SQL Server in a StatefulSet with persistent storage.
- Apply EF Core migrations through a Kubernetes Job before deploying the API.
- Verify readiness, Pod replacement, data persistence, scaling, and rollout behavior.

## Repository layout

| Path | Contents |
|---|---|
| `Kubernetes_IaC_DotNet_SQLServer_Tutorial_EN.html` | English slides with commands and checkpoints. |
| `src/LabIaCK8s/CatalogoApi/` | API source, data model, migration runner, and Dockerfile. |
| `src/LabIaCK8s/k8s/` | Namespace, ConfigMap, SQL Server, migration Job, and API manifests. |
| `src/LabIaCK8s/scripts/Importar-Imagem-Kind.ps1` | Script for importing the locally built image into kind nodes. |
| `LICENSE` | MIT license. |

The `catalogo-api` Deployment runs the API. Its Service forwards port 80 to port 8080 in the container. The API connects to SQL Server through the internal address `sqlserver:1433`. SQL Server runs as a single-instance StatefulSet and uses an 8 GiB PVC named `dados-sqlserver-0`. The `db-migrate` Job uses the API image with the `--migrate` argument.

## Prerequisites

- Windows x64, PowerShell, Git, and the .NET 10 SDK.
- Docker Desktop with Linux containers or Rancher Desktop with `dockerd (moby)`. Enable local Kubernetes.
- Docker CLI and `kubectl`, a default StorageClass, disk space for images and database storage, and access to NuGet and container registries.

| Environment | Kubernetes context | Local image workflow |
|---|---|---|
| Docker Desktop with Kubeadm | `docker-desktop` | Build in the local Docker engine and proceed to the manifests. |
| Docker Desktop with kind | `docker-desktop` | After building, import the image into the nodes as shown on slide 21. |
| Rancher Desktop with Moby | `rancher-desktop` | Build in Rancher Desktop's Docker engine and proceed to the manifests. |

Check the provisioner in the Docker Desktop Kubernetes settings. Both Docker Desktop provisioners can use the same context name.

## Run the lab

### 1. Clone the repository and check your environment

```powershell
git clone https://github.com/Daniel-Lim-Apo/Tutorial-Lab-Kubernetes-IaC-DotNet-SQLServer.git
Set-Location .\Tutorial-Lab-Kubernetes-IaC-DotNet-SQLServer\src\LabIaCK8s
dotnet --list-sdks
docker version
docker context show
kubectl config get-contexts
```

Run the context command that matches your environment:

```powershell
# Docker Desktop
kubectl config use-context docker-desktop

# Rancher Desktop
kubectl config use-context rancher-desktop
```

```powershell
kubectl get nodes
kubectl get storageclass
```

Confirm that the node is `Ready`, a StorageClass is marked `(default)`, and the Docker CLI points to the engine for your selected environment.

### 2. Restore, build, and generate the initial migration

Run the remaining project commands from `src/LabIaCK8s`.

```powershell
dotnet restore .\CatalogoApi\CatalogoApi.csproj --use-lock-file
dotnet build .\CatalogoApi\CatalogoApi.csproj --no-restore
```

If the local tool manifest does not exist, create it. If it already lists `dotnet-ef`, run `dotnet tool restore` instead of reinstalling the tool.

```powershell
dotnet new tool-manifest
dotnet tool install dotnet-ef --version "10.0.*"
```

If the initial migration has not already been generated, run:

```powershell
dotnet ef migrations add Inicial --project .\CatalogoApi --startup-project .\CatalogoApi
```

The migration files describe the schema changes. The Kubernetes Job applies them to SQL Server.

### 3. Build the image

```powershell
docker build --platform linux/amd64 --provenance=false `
  -f .\CatalogoApi\Dockerfile -t tutorialk8s-catalogo-api:1.0 .
```

The final dot sets the build context to the current directory. The Dockerfile expects `CatalogoApi/` inside that context. If your cluster uses kind, follow slide 21 to import the image before creating the Job.

### 4. Start the database, apply migrations, and deploy the API

```powershell
kubectl apply -f .\k8s\00-namespace.yaml
kubectl apply -f .\k8s\01-configmap.yaml
```

Follow **slide 23** to create the `banco-secrets` Secret in `iac-tutorialk8s` with the `SA_PASSWORD` key. Use a password dedicated to the lab and keep it out of Git. If you reuse an initialized volume, the password must match the existing SQL Server login.

```powershell
kubectl apply -f .\k8s\02-sqlserver.yaml
kubectl -n iac-tutorialk8s wait --for=condition=Ready pod/sqlserver-0 --timeout=300s
kubectl -n iac-tutorialk8s get pods,pvc
```

With the SQL Server Pod ready and the PVC `Bound`, apply the migration Job:

```powershell
kubectl apply -f .\k8s\03-migration-job.yaml
kubectl -n iac-tutorialk8s wait --for=condition=complete job/db-migrate --timeout=300s
kubectl -n iac-tutorialk8s logs job/db-migrate
```

After the Job completes, deploy the API:

```powershell
kubectl apply -f .\k8s\04-api.yaml
kubectl -n iac-tutorialk8s rollout status deployment/catalogo-api --timeout=180s
kubectl -n iac-tutorialk8s port-forward service/catalogo-api 8080:80
```

### 5. Test the API

Keep the port-forward running and open a second PowerShell terminal. The original API uses Portuguese route and field names.

```powershell
$base = "http://localhost:8080"
Invoke-RestMethod "$base/health/ready"

$product = @{ nome = "Notebook"; preco = 19.90 } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri "$base/produtos" -ContentType "application/json" -Body $product
Invoke-RestMethod "$base/produtos"
```

Before the first insert, `GET /produtos` returns an empty list. PowerShell may display no output for that list.

## Experiments and troubleshooting

- **Persistence.** Save a product, replace the SQL Server Pod as shown in the slides, and verify the product after the API is ready again. Preserve the PVC during the test.
- **Scaling.** Change the API Deployment's replica count and inspect the Pods and EndpointSlices. Each API replica uses the same SQL Server instance.
- **Update and rollback.** Follow slide 37 to build `:1.1` and use that exact image tag in the `api` container in `k8s/04-api.yaml`. Restart port-forwarding after the rollout and check the version at the root endpoint. To roll back, restore `:1.0` and reapply the manifest.
- **ImagePullBackOff.** Check the image tag, Docker engine, Kubernetes context, and image import if using kind.
- **Migration or HTTP 500 errors.** Check the migration, API, and database logs. After replacing the database Pod, confirm API readiness before retrying.

```powershell
kubectl -n iac-tutorialk8s get pods,pvc,jobs
kubectl -n iac-tutorialk8s get events --sort-by=.metadata.creationTimestamp
kubectl -n iac-tutorialk8s logs job/db-migrate
kubectl -n iac-tutorialk8s logs deployment/catalogo-api --tail=60
kubectl -n iac-tutorialk8s logs pod/sqlserver-0 --tail=60
```

## Cleanup

Press Ctrl+C to stop port-forwarding. To keep the data, follow the cleanup slide and scale the workloads to zero. When you can discard the data, remove the namespace:

```powershell
kubectl delete namespace iac-tutorialk8s
```

This removes the PVC. A `Delete` reclaim policy can also remove the backing storage.

The lab uses `sa` and `TrustServerCertificate=true` for local practice. Production deployments need scoped identities, certificate validation, backups, and an availability strategy.

## License

Distributed under the [MIT License](LICENSE).
