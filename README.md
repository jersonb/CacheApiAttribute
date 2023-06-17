# CacheApiAttribute
## Cache configuration in api by attribute

```yaml
version: '3.1'

services:

  rediscache:
    image: redis/redis-stack-server:latest
    restart: always
    ports:
      - 6379:6379
      - 13333:8001

```
