# Offline map sample

`natural-earth-110m.geojson` contains all 177 features from Natural Earth's
1:110m Admin-0 countries data. All geometry coordinates are retained. The source
`ADM0_A3` becomes the feature ID; only name and source population properties are
retained. The population field is source historical data, not a current estimate.

Made with Natural Earth. The data are public domain:
[terms](https://www.naturalearthdata.com/about/terms-of-use/).
The exact upstream commit, original and embedded SHA-256 hashes, and transformation
are recorded in `provenance.json`. The original download is retained with C6-03
validation evidence. The Demo loads this embedded asset without network access.

The small polygons and seat geometry used by mathematical tests and the custom
shape example are independently authored synthetic fixtures, not geographic data.
