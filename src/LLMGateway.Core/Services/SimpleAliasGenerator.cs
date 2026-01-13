using LLMGateway.Core.Interfaces;

namespace LLMGateway.Core.Services;

public class SimpleAliasGenerator : IAliasGenerator
{
    public string GenerateAlias(string prefix, int counter)
    {
        return $"{prefix}_{counter}";
    }
}

