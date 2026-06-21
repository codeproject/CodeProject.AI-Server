# Docker mesh observability and sensitive source isolation

This note describes a conservative Docker pattern for running several
CodeProject.AI Server instances as a local mesh while keeping project data,
module state, and observability boundaries easy to inspect.

The pattern is documentation-only. It does not require a server code change, a
new module, or a specific monitoring vendor.

## Why use this pattern?

CodeProject.AI Server is often used with local inputs such as camera frames,
screenshots, documents, OCR requests, or object detection images. These inputs
can contain private information even when they are "just images".

When several CodeProject.AI servers are connected through mesh, operators need
to know:

- which containers are part of the mesh;
- which server owns a module's persistent state;
- whether TCP and UDP port `32168` are available;
- which logs are safe to collect;
- whether observability tools are only collecting metadata, not request bodies.

## Recommended Docker layout

Use one shared mesh network and one private network per project or workload.
Give each server its own named persistent volume.

```yaml
name: cpai-studycase-mesh

x-cpai-common: &cpai-common
  image: codeproject/ai-server
  restart: unless-stopped
  networks:
    - cpai-mesh
  environment:
    MeshOptions__Enable: "true"
    MeshOptions__EnableBroadcasting: "true"
    MeshOptions__MonitorNetwork: "true"
    MeshOptions__AcceptForwardedRequests: "true"
    MeshOptions__AllowRequestForwarding: "true"

services:
  cpai-project-a:
    <<: *cpai-common
    container_name: cpai-project-a
    hostname: cpai-project-a
    ports:
      - "33168:32168/tcp"
      - "33168:32168/udp"
    environment:
      MeshOptions__KnownMeshHostnames__0: "cpai-project-b"
      MeshOptions__KnownMeshHostnames__1: "cpai-project-c"
    labels:
      cpai.mesh.block: "project-a"
      cpai.mesh.role: "analysis-server"
    networks:
      - cpai-mesh
      - project-a-private
    volumes:
      - cpai_project_a_state:/etc/codeproject/ai

  cpai-project-b:
    <<: *cpai-common
    container_name: cpai-project-b
    hostname: cpai-project-b
    ports:
      - "33268:32168/tcp"
      - "33268:32168/udp"
    environment:
      MeshOptions__KnownMeshHostnames__0: "cpai-project-a"
      MeshOptions__KnownMeshHostnames__1: "cpai-project-c"
    labels:
      cpai.mesh.block: "project-b"
      cpai.mesh.role: "analysis-server"
    networks:
      - cpai-mesh
      - project-b-private
    volumes:
      - cpai_project_b_state:/etc/codeproject/ai

  cpai-project-c:
    <<: *cpai-common
    container_name: cpai-project-c
    hostname: cpai-project-c
    ports:
      - "33368:32168/tcp"
      - "33368:32168/udp"
    environment:
      MeshOptions__KnownMeshHostnames__0: "cpai-project-a"
      MeshOptions__KnownMeshHostnames__1: "cpai-project-b"
    labels:
      cpai.mesh.block: "project-c"
      cpai.mesh.role: "analysis-server"
    networks:
      - cpai-mesh
      - project-c-private
    volumes:
      - cpai_project_c_state:/etc/codeproject/ai

networks:
  cpai-mesh:
    driver: bridge
  project-a-private:
    driver: bridge
  project-b-private:
    driver: bridge
  project-c-private:
    driver: bridge

volumes:
  cpai_project_a_state:
  cpai_project_b_state:
  cpai_project_c_state:
```

## Mesh notes

- Map both TCP and UDP for `32168` when testing mesh behavior from the host.
- Use `MeshOptions__KnownMeshHostnames__*` in Docker if broadcast discovery is
  unreliable.
- Keep module state in a named volume per server so different workloads do not
  overwrite each other.
- Do not assume every server has the same modules, models, or hardware.
- If a route should stay on one server, check that the corresponding module
  route is not unintentionally mesh-enabled.

## Optional observability

Docker labels can make a multi-server mesh much easier to inspect. An external
observability agent can collect container health, restart count, CPU, memory,
and explicitly labelled logs without joining the CodeProject.AI mesh network.

If you add a monitoring container, keep it on a separate observability network
and let it read Docker metadata through Docker's supported mechanisms.

```yaml
services:
  observability-agent:
    image: your-observability-agent-image
    restart: unless-stopped
    networks:
      - observability-private
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro
    environment:
      LOG_COLLECTION_MODE: "explicit-labels-only"

networks:
  observability-private:
    driver: bridge
```

The agent should not become another mesh participant. It should observe
containers and labels, not process AI requests.

## Sensitive source safety

Avoid collecting or forwarding raw sensitive payloads through logs or monitoring
tags.

Do not send these values to logs or observability tools:

- raw images;
- OCR text;
- API keys;
- private keys;
- `.env` values;
- request bodies;
- authentication headers;
- license plates, faces, or document screenshots unless explicitly redacted.

Prefer logging metadata such as:

- container name;
- route name;
- module id;
- request success or failure;
- latency;
- queue length;
- sanitized error class.

## Validation checklist

Before sharing a mesh example or asking for help, capture these checks:

```powershell
docker compose config --quiet
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
docker network inspect cpai-studycase-mesh_cpai-mesh
```

Then verify the servers respond:

```text
http://localhost:33168
http://localhost:33268
http://localhost:33368
```

Finally, confirm that any observability container is attached only to its own
observability network and not to the CodeProject.AI mesh network.

