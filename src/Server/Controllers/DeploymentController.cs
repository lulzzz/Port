﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Port.Shared;

namespace Port.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DeploymentController : ControllerBase
    {
        private readonly IKubernetesService _kubernetesService;

        public DeploymentController(IKubernetesService kubernetesService)
        {
            _kubernetesService = kubernetesService;
        }

        [HttpGet("{context}")]
        public async Task<IEnumerable<Deployment>> Get(string context)
        {
            return await _kubernetesService.ListDeploymentsInAllNamespacesAsync(context)
                .ConfigureAwait(false);
        }
    }
}
