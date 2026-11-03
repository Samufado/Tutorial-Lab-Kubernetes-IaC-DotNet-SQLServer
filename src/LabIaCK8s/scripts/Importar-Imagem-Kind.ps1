# Opcional: somente se o Docker Desktop estiver usando o provisionador kind.
# Execute a partir da raiz LabIaCK8s, apos docker build.
$ErrorActionPreference = "Stop"
if ((kubectl config current-context) -ne "docker-desktop") {
    throw "Selecione o contexto docker-desktop antes de continuar."
}
$nodes = (kubectl get nodes -o json | ConvertFrom-Json).items.metadata.name
$containers = @(docker ps --format '{{.Names}}')
$targets = @($nodes | Where-Object { $containers -contains $_ })
if ($targets.Count -eq 0) {
    throw "Nao encontrei nos kind como conteineres. Este passo e apenas para kind."
}
docker image save --platform linux/amd64 --output tutorialk8s-api.tar tutorialk8s-catalogo-api:1.0
if ($LASTEXITCODE -ne 0) { throw "Falha ao exportar a imagem." }
foreach ($node in $targets) {
    docker cp .\tutorialk8s-api.tar "${node}:/tmp/tutorialk8s-api.tar"
    if ($LASTEXITCODE -ne 0) { throw "Falha na copia para $node." }
    docker exec $node ctr --namespace k8s.io images import --all-platforms /tmp/tutorialk8s-api.tar
    if ($LASTEXITCODE -ne 0) { throw "Falha na importacao para $node." }
    docker exec $node rm /tmp/tutorialk8s-api.tar
}
Remove-Item .\tutorialk8s-api.tar
