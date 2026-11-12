# Run after Start-Demo.ps1. Both DTO types are sent in both directions.
$animal = '{"name":"Dog","age":3}'
$phone = '{"brand":"Apple","model":"iPhone"}'
Invoke-RestMethod 'http://localhost:5000/animal/External-01' -Method Post -ContentType 'application/json' -Body $animal
Invoke-RestMethod 'http://localhost:5000/phone/all' -Method Post -ContentType 'application/json' -Body $phone
Invoke-RestMethod 'http://localhost:5101/push/animal' -Method Post -ContentType 'application/json' -Body $animal
Invoke-RestMethod 'http://localhost:5101/push/phone' -Method Post -ContentType 'application/json' -Body $phone
