using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Shared.Abstractions.Extensions
{
    public static class PropertyBuilderExtensions
    {
        public static PropertyBuilder<Uri> HasUriConversion(this PropertyBuilder<Uri> builder)
        {
            return builder.HasConversion(
                uri => uri.ToString(),
                str => new Uri(str));
        }
    }
}
