using System.Text.Json;
using FormCMS.Auth.ApiClient;
using FormCMS.CoreKit.ApiClient;
using FormCMS.Core.Descriptors;
using FormCMS.Utils.ResultExt;
using FormCMS.CoreKit.Test;
using FormCMS.Utils.DisplayModels;
using FormCMS.Utils.EnumExt;
using FormCMS.Utils.jsonElementExt;
using Microsoft.Extensions.Primitives;
using Namotion.Reflection;
using NUlid;
using Attribute = FormCMS.Core.Descriptors.Attribute;

namespace FormCMS.Course.Tests;

public class QueryApiTest
{
    private readonly string _queryName = "qt_query_" + Ulid.NewUlid();
    private readonly  string _post = "qt_post_" + Ulid.NewUlid();

    private readonly QueryApiClient _query;
    private readonly EntityApiClient _entity;
    private readonly SchemaApiClient _schema;
    private readonly BlogsTestCases _commonTestCases;

    public QueryApiTest()
    {
        Util.SetTestConnectionString();

        WebAppClient<Program> webAppClient = new();
        new AuthApiClient(webAppClient.GetHttpClient()).EnsureSaLogin().Ok().GetAwaiter().GetResult();
        _schema = new SchemaApiClient(webAppClient.GetHttpClient());

        var asset = new AssetApiClient(webAppClient.GetHttpClient());
        _entity= new EntityApiClient(webAppClient.GetHttpClient());
        _query = new QueryApiClient(webAppClient.GetHttpClient());
        _commonTestCases = new BlogsTestCases(_query, _queryName);


        if (_schema.ExistsEntity(TestEntityNames.TestPost.Camelize()).GetAwaiter().GetResult()) return;
        BlogsTestData.EnsureBlogEntities(_schema).GetAwaiter().GetResult();
        BlogsTestData.PopulateData(_entity,asset).Wait();
    }
     [Fact]
     public async Task TestCsv()
     {
         var items = await $$"""
                             query {
                                  {{TestEntityNames.TestPost.Camelize()}}List{
                                      id, {{TestFieldNames.Language.Camelize()}}
                                  }
                             }
                             """.GraphQlQuery<JsonElement[]>(_query).Ok();
         var first = items.First();
         var ele = first.GetProperty(TestFieldNames.Language.Camelize());
         Assert.True(ele.ValueKind == JsonValueKind.Array);
     }   
    [Fact]
    public async Task TestDictionary()
    {
        var items = await $$"""
                            query {
                                 {{TestEntityNames.TestPost.Camelize()}}List{
                                     id, 
                                     {{TestFieldNames.MetaData.Camelize()}}
                                 }
                            }
                            """.GraphQlQuery<JsonElement[]>(_query).Ok();
        var first = items.First();
        var ele = first.GetProperty(TestFieldNames.MetaData.Camelize());
        Assert.True(ele.TryGetProperty("Key",out var url));
    }
    
    [Fact]
    public async Task TestAssets()
    {
        var items = await $$"""
                            query {
                                 {{TestEntityNames.TestPost.Camelize()}}List{
                                     id, 
                                     title, 
                                     image {url, title},
                                     gallery {url, title}
                                 }
                            }
                            """.GraphQlQuery<JsonElement[]>(_query).Ok();
        var first = items.First();
        var image = first.GetProperty("image");
        Assert.True(image.TryGetProperty("url",out var url));
        Assert.True(image.TryGetProperty("title",out var title));
        
        var gallery = first.GetProperty("gallery");
        foreach (var jsonElement in gallery.EnumerateArray())
        {
            Assert.True(jsonElement.TryGetProperty("url", out var url1));
            Assert.True(jsonElement.TryGetProperty("title", out var title1));
        }
    }

    [Fact]
    public async Task TestDistinct()
    {
        var items = await $$$"""
                             query {
                                  {{{TestEntityNames.TestPost.Camelize()}}}List(filterExpr:{field:"tags.name",clause:{startsWith:"Name-1"}} ){
                                      id, title
                                  }
                             }
                             """.GraphQlQuery<JsonElement[]>(_query).Ok();
        Assert.True(items.Length > 1);
        items = await $$$"""
                             query {
                                  {{{TestEntityNames.TestPost.Camelize()}}}List(distinct:true, filterExpr:{field:"tags.name",clause:{startsWith:"Name-1"}} ){
                                      id, title
                                  }
                             }
                             """.GraphQlQuery<JsonElement[]>(_query).Ok();
        Assert.True(items.Length == 1);
    }
    
    
    [Fact]
    public async Task EnsureDraftEntitySchemaNotAffectQuery()
    {
        //the first entity is published
        Attribute[] attrs = [
            new ("name", "Name", DisplayType: DisplayType.Text),
            new ("name1", "Name1", DisplayType: DisplayType.Text)
        ];
        await _schema.EnsureEntity(_post, "name", false, attrs).Ok();
        
        var payload = new Dictionary<string, object>
        {
            {"name","post21"},
            {"name1","post22"},
        };
        await _entity.Insert(_post, payload);

        await $$"""
                query {{_queryName}}{
                   {{_post}}List{id, name, name1}
                }
                """.GraphQlQuery(_query).Ok();


        
        // remove name1 this latest version is draft
        await _schema.EnsureSimpleEntity(_post, "name", false).Ok();
        await _entity.Insert(_post, "name", "post1").Ok();// should be draft now
        
       
        //query use published schema, so it is still working
        var items = await _query.List(_queryName).Ok();
        Assert.Equal(2,items.Length);
        
        //use draft post, so should fail
        var args = new Dictionary<string, StringValues>
        {
            {"sandbox","1"}
        };
        var res =await _query.List(_queryName, args);
        Assert.True(res.IsFailed);
    }
    
    [Fact]
    public async Task EnsureDaftDataNotAffectQuery()
    {
        await _schema.EnsureSimpleEntity(_post, "name", true).Ok();
        await _entity.Insert(_post, "name", "post1");// should be draft now
        
        await $$"""
                query {{_queryName}}{
                   {{_post}}List{id }
                }
                """.GraphQlQuery(_query).Ok();
        var items = (await _query.List(_queryName)).Ok();
        Assert.Empty(items);
        
        var args = new Dictionary<string, StringValues>
        {
            {"preview","1"}
        };
        items = await _query.List(_queryName, args).Ok();
        Assert.Single(items);
    }


    [Fact]
    public Task VerifyValueSetMatch() => _commonTestCases.Filter.ValueSetMatch();
    [Fact]
    public Task VerifyMatchAllCondition() => _commonTestCases.Filter.MatchAllCondition();
    [Fact]
    public Task VerifyMatchAnyCondition() => _commonTestCases.Filter.MatchAnyCondition();
    [Fact]
    public Task VerifyFilterExpression() => _commonTestCases.Filter.VerifyFilterExpression();

    [Fact]
    public Task VerifyRecordCount() => _commonTestCases.SavedQuery.VerifyRecordCount();

    [Fact]
    public Task PaginationByCurosr() => _commonTestCases.SavedQuery.PaginationByCursor();

    [Fact]
    public Task VerifyManyApi() => _commonTestCases.SavedQuery.VerifyManyApi();

    [Fact]
    public Task VerifySingleApi() => _commonTestCases.SavedQuery.VerifySingleApi();

    
    [Fact]
    public Task VerifySort() => _commonTestCases.Sort.VerifySort();

    [Fact]
    public Task VerifySortExpression() => _commonTestCases.Sort.VerifySortExpression();


    [Fact]
    public Task VerifySingleGraphQlQuery() => _commonTestCases.RealtimeQueryTest.SingleGraphQlQuery();

    [Fact]
    public Task VerifyComplexFieldSelection() => _commonTestCases.RealtimeQueryTest.ComplexFieldSelection();
    
    [Fact]
    public Task VerifyPagination() => _commonTestCases.RealtimeQueryTest.RealtimeQueryPagination();

    [Fact]
    public Task VariableInSet() => _commonTestCases.Variable.ValueInSet();

    [Fact]
    public Task VariableStartsWith() => _commonTestCases.Variable.StartsWith();

    [Fact]
    public Task VariableFilterExpression() => _commonTestCases.Variable.FilterExpression();
    
    [Fact]
    public Task VariableSort() => _commonTestCases.Variable.Sort();
    [Fact]
    public Task VariableSortExpression() => _commonTestCases.Variable.SortExpression();
    [Fact]
    public Task VariablePagination() => _commonTestCases.Variable.Pagination();
    
    [Fact] 
    public Task FilterByPublishedAtRealTime() => _commonTestCases.RealtimeQueryTest.FilterByPublishedAt();
    
    [Fact] 
    public Task FilterByPublishedAtSaved() => _commonTestCases.SavedQuery.FilterByPublishedAt();

    [Fact]
    public Task FilterByPublishedVariable() => _commonTestCases.Variable.FilterByPublishedAt();
    
    [Fact]
    public Task CollectionPart() => QueryParts("attachments", ["id", "post"]);

    [Fact]
    public Task JunctionPart() => QueryParts("tags", ["id"]);


    private async Task QueryParts(string attrName, string[] attrs)
    {
        const int limit = 4;
        await $$"""
                query {{_queryName}}{
                   {{TestEntityNames.TestPost.Camelize()}}List{
                      id
                      {{attrName}}{
                          {{string.Join(",", attrs)}}
                      }
                   }
                }
                """.GraphQlQuery(_query).Ok();

        var posts = (await _query.List(_queryName, new Dictionary<string, StringValues>
        {
            [$"{attrName}.limit"] = limit.ToString(),
            [$"limit"] = "1",
        })).Ok();

        var post = posts[0].ToDictionary();
        if (post.TryGetValue(attrName, out var v)
            && v is object[] arr
            && arr.Last() is Dictionary<string, object> last)
        {

            var cursor = SpanHelper.Cursor(last);
            var items = (await _query.Part(query: _queryName, attr: attrName, last: cursor, limit: limit)).Ok();
            Assert.Equal(limit, items.Length);
        }
        else
        {
            Assert.Fail("Failed to find last cursor");
        }
    }
}