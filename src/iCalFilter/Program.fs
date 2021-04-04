module iCalFilter.App

open System
open System.Net
open System.Net.Http
open System.Text.RegularExpressions
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe
open GiraffeViewEngine
open FSharp.Control.Tasks.V2.ContextInsensitive
open Ical.Net
open Ical.Net.Serialization

module Int =
    let tryParse (str : string) =
        match Int32.TryParse str with
        | (true, i) -> Some i
        | (false, _) -> None

module List =
    let rec chooseAll = function
        | [] -> Some []
        | Some x :: xs -> chooseAll xs |> Option.map (fun rest -> x::rest)
        | None :: _ -> None

let handleFilter : HttpHandler = fun next ctx -> task {
    let url = ctx.TryGetQueryStringValue("url")
    let days = ctx.TryGetQueryStringValue("days")
    let nameRegex = ctx.TryGetQueryStringValue("nameregex")
    let descriptionRegex = ctx.TryGetQueryStringValue("descriptionregex")

    let optRegexFilter regex evFieldSelector events =
        match regex with
        | Some regex ->
            events 
            |> Seq.filter (fun ev -> 
                Regex.IsMatch(
                    evFieldSelector ev, 
                    regex, 
                    RegexOptions.CultureInvariant ||| RegexOptions.ExplicitCapture, 
                    TimeSpan.FromMilliseconds(300.)
                ))
        | None ->
            events

    match url, days with
    | Some url, Some days ->
        let url = WebUtility.UrlDecode(url)
        if not (Uri.IsWellFormedUriString(url, UriKind.Absolute)) then
            return! RequestErrors.badRequest (text "url is not a well formed uri") next ctx
        else 
            let days = 
                days.Split(',', StringSplitOptions.RemoveEmptyEntries)
                |> List.ofArray
                |> List.map (Int.tryParse >> Option.map enum<DayOfWeek>)
                |> List.chooseAll
            
            match days with
            | Some days ->
                let http = ctx.GetService<IHttpClientFactory>().CreateClient()
                let! resp = http.GetAsync(url)
                let! data = resp.Content.ReadAsStringAsync()
                if resp.IsSuccessStatusCode then
                    let ical = Calendar.Load(data)
                    let events = 
                        ical.Events 
                        |> Seq.filter (fun ev -> days |> List.contains ev.DtStart.DayOfWeek)
                        |> optRegexFilter nameRegex (fun ev -> ev.Name)
                        |> optRegexFilter descriptionRegex (fun ev -> ev.Description)
                        |> Seq.toList
                    ical.Events.Clear()
                    ical.Events.AddRange(events)

                    let serializer = CalendarSerializer()
                    return! Successful.ok (text (serializer.SerializeToString(ical))) next ctx
                else 
                    let resp = sprintf "Received status %O from %s. Original content follows.\n\n%s" resp.StatusCode url data
                    return! ServerErrors.badGateway (text resp) next ctx
            | None ->
                return! RequestErrors.badRequest (text "days is not a valid comma separated list of ints") next ctx
    | _ ->
        return! RequestErrors.badRequest (text "url and days query parameters are required") next ctx
}

let index (ctx : HttpContext) =
    let config = ctx.GetService<IConfiguration>()
    html [ attr "data-host" (config.GetValue("host")) ] [
        head [] [
            title [] [ str "iCal Filter" ]
            script [ _src "/script.js" ] []
        ]
        body [] [
            h1 [] [ str "iCal Filter" ]
            form [] [
                label [ _for "ical-url" ] [ str "iCal Url" ]
                input [ _type "url"; _id "ical-url" ]
                fieldset [] [
                    yield legend [] [ str "Choose the days to include in your calendar:" ]
                    for day in Enum.GetValues(typeof<DayOfWeek>) do
                        let day = day :?> DayOfWeek
                        let id = sprintf "day-%i" (int day)
                        yield input [ _type "checkbox"; _id id ]
                        yield label [ _for id ] [ str (string day) ]
                ]
                fieldset [] [
                    legend [] [ str "Name and description matching" ]
                    label [ _for "name-regex" ] [ str "Name matching regex" ]
                    input [ _type "text"; _id "name-regex" ]
                    br []
                    label [ _for "description-regex" ] [ str "Description matching regex" ]
                    input [ _type "text"; _id "description-regex" ]
                ]
                button [ _type "button"; _onclick "getCustomUrl()" ] [
                    str "Create Url"
                ]
            ]
            div [ _id "custom-url" ] []
        ]
    ]

let htmlViewCtx view next ctx =
    htmlView (view ctx) next ctx

let webApp =
    choose [
        GET >=> choose [
            route "/" >=> htmlViewCtx index
            route "/filter" >=> handleFilter
        ]
        RequestErrors.notFound (text "Not Found") 
    ]

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> ServerErrors.internalError (text ex.Message)

let configureCors (config : IConfiguration) (cors : CorsPolicyBuilder) =
    cors.AllowAnyOrigin()
        .WithMethods("GET")
        .AllowAnyHeader()
        |> ignore

let configureApp (app : IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IHostEnvironment>()
    let config = app.ApplicationServices.GetService<IConfiguration>()

    let useErrorHandler (app : IApplicationBuilder) = 
        if env.IsDevelopment() 
        then app.UseDeveloperExceptionPage()
        else app.UseGiraffeErrorHandler errorHandler

    (app |> useErrorHandler)
        .UseHttpsRedirection()
        .UseCors(configureCors config)
        .UseStaticFiles()
        .UseGiraffe(webApp)

let configureServices (services : IServiceCollection) =
    services.AddCors()
        .AddHttpClient() 
        .AddGiraffe()
        |> ignore

[<EntryPoint>]
let main args =
    WebHost.CreateDefaultBuilder(args)
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(configureServices)
        .Build()
        .Run()
    0
