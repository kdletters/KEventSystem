using Microsoft.CodeAnalysis;

namespace Kdletters.Analyzer
{
    public static class AnalyzerHelper
    {
        public static bool InheritsFrom(this INamedTypeSymbol symbol, ITypeSymbol type)
        {
            var baseType = symbol.BaseType;
            while (baseType != null)
            {
                if (type.Equals(baseType))
                    return true;

                baseType = baseType.BaseType;
            }

            return false;
        }
    }
}