$platform = $args[0]
if (!$platform) {
	Write-Host "Missing Platform: windows | linux"
	exit(1)
}

if ($platform -eq "linux") {
    # there's a bug on stopping/terminating LCOW
    # https://github.com/moby/moby/issues/37919
    # workaround is to kill the containers manually

    #Write-Host "LCOW stopping bug, hack: killing the containers (https://github.com/moby/moby/issues/37919)."
    #docker kill -s 9 nesci_sqlexpress_1
    #docker kill -s 9 nesci_mongo_1
    #docker kill -s 9 nesci_mysql_1
    #docker kill -s 9 nesci_postgres_1
}
# -v "removes all the volumes, there's no need to do it manually"
docker-compose -f docker-compose.ci.$platform.db.yml -p nesci down -v

# remove unneeded volumes, so we start clear every time
#Write-Host "Removing volumes:"
#docker volume rm nesci_h8_sqldata_${platform}_ci
#docker volume rm nesci_h8_mongodata_${platform}_ci