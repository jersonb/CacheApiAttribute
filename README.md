# CacheApiAttribute
## Cache configuration in api by attribute

> **OBS:** In a single class to simplify the explanation

### docker-compose
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
