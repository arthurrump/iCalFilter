function getCustomUrl() {
    console.log("click");
    var host = document.getElementsByTagName("html")[0].getAttribute("data-host");
    var path = "/filter";
    var icalUrl = encodeURIComponent(document.getElementById("ical-url").value);
    var days = "";
    for (let i = 0; i < 7; i++) {
        if (document.getElementById("day-" + i).checked) days += i + ","
    }
    var url = host + path + "?url=" + icalUrl + "&days=" + days;
    var nameRegex = document.getElementById("name-regex").value;
    if (nameRegex != undefined && nameRegex != null && nameRegex != "") {
        url = url + "&nameregex=" + encodeURIComponent(nameRegex);
    }
    var descriptionRegex = document.getElementById("description-regex").value;
    if (descriptionRegex != undefined && descriptionRegex != null && descriptionRegex != "") {
        url = url + "&descriptionregex=" + encodeURIComponent(descriptionRegex);
    }
    document.getElementById("custom-url").innerHTML =
        "Your custom Url: <a href=\"" + url + "\">" + url + "</a>";
}