function getCustomUrl() {
    console.log("click");
    var host = document.getElementsByTagName("html").getAttribute("data-host");
    var path = "/filter";
    var icalUrl = encodeURIComponent(document.getElementById("ical-url").value);
    var days = "";
    for (let i = 0; i < 7; i++) {
        if (document.getElementById("day-" + i).checked) days += i + ","
    }
    var url = host + path + "?url=" + icalUrl + "&days=" + days;
    document.getElementById("custom-url").innerHTML =
        "Your custom Url: <a href=\"" + url + "\">" + url + "</a>";
}