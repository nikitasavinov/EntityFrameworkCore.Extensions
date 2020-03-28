using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.SqlServer.Migrations.Internal;

namespace EntityFrameworkCore.Extensions.Services
{
    public class ExtendedSqlServerMigrationsAnnotationProvider : SqlServerMigrationsAnnotationProvider
    {
        public ExtendedSqlServerMigrationsAnnotationProvider(MigrationsAnnotationProviderDependencies dependencies) : base(dependencies)
        {
        }

        public override IEnumerable<IAnnotation> For(IProperty property) => base.For(property)
                                                                                .Concat(property.GetAnnotations()
                                                                                                .Where(t => t.Name == AnnotationConstants.DynamicDataMasking));
    }
}
