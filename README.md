# Laboratório de Kubernetes e IaC com .NET e SQL Server

**Professor Daniel Lim-Apo**

Neste laboratório, você publica uma API de catálogo de produtos em um cluster Kubernetes local. A aplicação usa ASP.NET Core com .NET 10, C# e Entity Framework Core. Os manifestos YAML descrevem a infraestrutura como código (IaC), do armazenamento do SQL Server à atualização da API.

[Abra o tutorial completo em slides HTML](Laboratorio_IaC_Kubernetes_Dotnet_SQLServer_tutorialk8s.html). Baixe o HTML e abra-o no navegador para usar o sumário, a navegação e os botões de cópia. Os comandos abaixo resumem a sequência para quem já está com o projeto aberto.

## O que você vai praticar

- Construir uma imagem de aplicação em dois estágios com Docker.
- Separar configurações e credenciais com ConfigMap e Secret.
- Executar o SQL Server em um StatefulSet e preservar os dados em um volume persistente.
- Aplicar as migrations do EF Core em um Job antes de publicar a API.
- Observar prontidão, reconciliação, escalabilidade e atualização de um Deployment.
- Acessar a API pelo PowerShell e verificar se os dados persistem após recriar o Pod do banco.

## Estrutura do repositório

| Caminho | Conteúdo |
|---|---|
| `Laboratorio_IaC_Kubernetes_Dotnet_SQLServer_tutorialk8s.html` | Roteiro em slides, com explicações e checkpoints. |
| `src/LabIaCK8s/CatalogoApi/` | Código da API, modelo, DbContext, rotas, migrador e Dockerfile. |
| `src/LabIaCK8s/k8s/` | Manifestos de namespace, configuração, SQL Server, Job de migração e API. |
| `src/LabIaCK8s/scripts/Importar-Imagem-Kind.ps1` | Importação da imagem local para os nós de um cluster kind. |
| `LICENSE` | Licença MIT. |

A API é executada pelo Deployment `catalogo-api`. Seu Service recebe tráfego na porta 80 e o encaminha para a porta 8080 do contêiner. A API acessa o SQL Server pelo nome interno `sqlserver`, na porta 1433. O StatefulSet mantém uma instância do banco e usa o PVC `dados-sqlserver-0` de 8 GiB. O Job `db-migrate` usa a imagem da API com o argumento `--migrate`.

## Pré-requisitos

- Windows x64 com PowerShell, Git e SDK .NET 10.
- Docker Desktop em modo de contêineres Linux ou Rancher Desktop com `dockerd (moby)`, com Kubernetes habilitado.
- Docker CLI e `kubectl` disponíveis no terminal.
- Acesso ao NuGet e aos registries das imagens base, uma StorageClass padrão e espaço em disco para as imagens e o volume do banco.

| Ambiente | Contexto esperado | Imagem local |
|---|---|---|
| Docker Desktop com Kubeadm | `docker-desktop` | Construa no motor local e avance para os manifestos. |
| Docker Desktop com kind | `docker-desktop` | Após o build, siga o slide 21 para importar a imagem nos nós. |
| Rancher Desktop com Moby | `rancher-desktop` | Construa no motor do Rancher Desktop e avance para os manifestos. |

Confira o provisionador na interface do Docker Desktop. O contexto `docker-desktop` pode aparecer tanto com Kubeadm quanto com kind.

## Execute o laboratório

### 1. Clone e confira o ambiente

```powershell
git clone https://github.com/Daniel-Lim-Apo/Tutorial-Lab-Kubernetes-IaC-DotNet-SQLServer.git
Set-Location .\Tutorial-Lab-Kubernetes-IaC-DotNet-SQLServer\src\LabIaCK8s
dotnet --list-sdks
docker version
docker context show
kubectl config get-contexts
```

Escolha o contexto que corresponde ao seu ambiente. Execute apenas a linha correspondente.

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

Confirme que o nó está `Ready`, que há uma StorageClass marcada como `(default)` e que o Docker CLI aponta para o motor do mesmo ambiente em que você vai executar a API.

### 2. Compile e gere a migration inicial

Todos os comandos de build e aplicação de arquivos partem de `src/LabIaCK8s`.

```powershell
dotnet restore .\CatalogoApi\CatalogoApi.csproj --use-lock-file
dotnet build .\CatalogoApi\CatalogoApi.csproj --no-restore
```

Se ainda não houver um manifesto local de ferramentas, crie-o e instale o EF CLI:

```powershell
dotnet new tool-manifest
dotnet tool install dotnet-ef --version "10.0.*"
```

Se a configuração da ferramenta já estiver no manifesto, execute `dotnet tool restore`. Gere a migration inicial uma vez, caso ela ainda não exista:

```powershell
dotnet ef migrations add Inicial --project .\CatalogoApi --startup-project .\CatalogoApi
```

A migration gerada descreve as mudanças de esquema. O Job do Kubernetes aplicará essas mudanças ao SQL Server.

### 3. Construa a imagem

```powershell
docker build --platform linux/amd64 --provenance=false `
  -f .\CatalogoApi\Dockerfile -t tutorialk8s-catalogo-api:1.0 .
```

O ponto final indica a pasta atual como contexto de build. O Dockerfile usa caminhos que partem da pasta `LabIaCK8s`. Se o seu provisionador é kind, importe a imagem conforme o slide 21 antes de criar o Job.

### 4. Suba o SQL Server, aplique a migration e publique a API

```powershell
kubectl apply -f .\k8s\00-namespace.yaml
kubectl apply -f .\k8s\01-configmap.yaml
```

Siga o **slide 23** para criar o Secret `banco-secrets` no namespace `iac-tutorialk8s`, com a chave `SA_PASSWORD`. Use uma senha exclusiva para o laboratório e mantenha-a fora do Git. Se reutilizar um volume já inicializado, use a senha correspondente ao login existente no banco.

```powershell
kubectl apply -f .\k8s\02-sqlserver.yaml
kubectl -n iac-tutorialk8s wait --for=condition=Ready pod/sqlserver-0 --timeout=300s
kubectl -n iac-tutorialk8s get pods,pvc
```

Com o Pod pronto e o PVC `Bound`, execute o Job:

```powershell
kubectl apply -f .\k8s\03-migration-job.yaml
kubectl -n iac-tutorialk8s wait --for=condition=complete job/db-migrate --timeout=300s
kubectl -n iac-tutorialk8s logs job/db-migrate
```

Depois que o Job concluir, publique a API:

```powershell
kubectl apply -f .\k8s\04-api.yaml
kubectl -n iac-tutorialk8s rollout status deployment/catalogo-api --timeout=180s
kubectl -n iac-tutorialk8s port-forward service/catalogo-api 8080:80
```

### 5. Teste a API

Mantenha o encaminhamento aberto e execute em outro PowerShell:

```powershell
$base = "http://localhost:8080"
Invoke-RestMethod "$base/health/ready"

$produto = @{ nome = "Caderno"; preco = 19.90 } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri "$base/produtos" -ContentType "application/json" -Body $produto
Invoke-RestMethod "$base/produtos"
```

Antes do primeiro cadastro, `GET /produtos` retorna uma lista vazia. O PowerShell pode exibir essa lista sem conteúdo visível no terminal.

## Experimentos e diagnóstico

- **Persistência.** Cadastre um produto, recrie o Pod `sqlserver-0` conforme os slides e confirme que o produto reaparece quando a API estiver pronta. Preserve o PVC durante o teste.
- **Escalabilidade.** Edite `replicas` no Deployment da API e confira os Pods e EndpointSlices. Todas as réplicas consultam o mesmo banco.
- **Atualização.** No slide 37, construa a imagem `:1.1` e configure a mesma tag em `k8s/04-api.yaml`, no contêiner `api`. Após o rollout, reabra o encaminhamento e consulte a raiz da API para conferir a versão.
- **ImagePullBackOff.** Confira a tag no YAML, a existência da imagem no motor local e, se estiver usando kind, a importação nos nós.
- **Falha de migração ou HTTP 500.** Consulte os logs do Job, da API e do SQL Server. Após recriar o banco, espere a prontidão da API antes de repetir a consulta.

```powershell
kubectl -n iac-tutorialk8s get pods,pvc,jobs
kubectl -n iac-tutorialk8s get events --sort-by=.metadata.creationTimestamp
kubectl -n iac-tutorialk8s logs job/db-migrate
kubectl -n iac-tutorialk8s logs deployment/catalogo-api --tail=60
```

## Encerramento

Pressione Ctrl+C no terminal do encaminhamento. Se quiser preservar os dados para retomar depois, siga a etapa de limpeza dos slides que escala as cargas para zero. Para excluir o laboratório inteiro, use o comando abaixo somente quando puder descartar os dados. A exclusão do namespace remove o PVC e, com uma política de recuperação `Delete`, pode apagar o armazenamento associado.

```powershell
kubectl delete namespace iac-tutorialk8s
```

O exemplo usa `sa` e `TrustServerCertificate=true` para a execução local. Em produção, use identidades com privilégio mínimo, certificados verificáveis e uma estratégia de backup e disponibilidade.

## Licença

Este projeto é distribuído sob a [licença MIT](LICENSE).
