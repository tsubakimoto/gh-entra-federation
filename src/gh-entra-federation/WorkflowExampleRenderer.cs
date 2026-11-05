namespace GhEntraFederation;

public static class WorkflowExampleRenderer
{
    public static string Render(string environmentName)
    {
        const string template = """
name: deploy
on:
  workflow_dispatch:
permissions:
  id-token: write
  contents: read
jobs:
  deploy:
    runs-on: ubuntu-latest
    environment: __ENVIRONMENT_NAME__
    steps:
      - uses: actions/checkout@v4
      - uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
      - name: Verify account
        run: az account show
""";
        return template.Replace("__ENVIRONMENT_NAME__", environmentName, StringComparison.Ordinal);
    }
}
