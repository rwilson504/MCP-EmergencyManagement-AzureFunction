$envName = "raw-dev2"
$loc = "eastus"

# Build
az bicep build --file infra/main.bicep --outdir infra/_build
$localHash = (Get-Content infra/_build/main.json -Raw | ConvertFrom-Json).metadata._generator.templateHash
"Local templateHash: $localHash"

# What-if
az deployment sub what-if --name "${envName}-preview" --location $loc `
  --template-file infra/main.bicep --parameters environmentName=$envName location=$loc

# Show last deployment hash (guessing name 'main'—adjust if needed)
$depName = "main"
$remoteHash = az deployment sub show --name $depName --query "properties.templateHash" -o tsv 2>$null
if ($remoteHash) { "Remote templateHash: $remoteHash" }