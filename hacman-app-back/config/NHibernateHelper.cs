using hacman_app_back.Modules.Identity.Infrastructure;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Dialect;
using NHibernate.Driver;
using NHibernate.Mapping.ByCode;

namespace hacman_app_back.config;

public class NHibernateHelper
{
    public static Configuration BuildConfiguration(string connectionString, string schema)
    {
        var cfg = new Configuration();

        cfg.DataBaseIntegration(db =>
            {
                db.Dialect<PostgreSQL83Dialect>();
                db.Driver<NpgsqlDriver>();
                db.ConnectionString = connectionString;
                db.Timeout = 30;
                db.KeywordsAutoImport = Hbm2DDLKeyWords.AutoQuote;
                db.LogFormattedSql = true;
            }
        );

        cfg.SetProperty("default_schema", schema);
        var mapper = new ModelMapper();
        mapper.AddMapping<UserMap>();
        var mapping = mapper.CompileMappingForAllExplicitlyAddedEntities();
        cfg.AddDeserializedMapping(mapping, "hacman");

        return cfg;
    }

    public static ISessionFactory CreateSessionFactory(string connectionString, string schema)
        => BuildConfiguration(connectionString, schema).BuildSessionFactory();
}