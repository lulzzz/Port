﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Port.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PodController : ControllerBase
    {
        private readonly IKubernetesService _kubernetesService;

        public PodController(
            IKubernetesService kubernetesService)
        {
            _kubernetesService = kubernetesService;
        }

        [HttpGet("{context}")]
        public async Task<IEnumerable<Shared.Pod>> GetAsync(
            string context)
        {
            return await _kubernetesService.ListPodsInAllNamespacesAsync(
                context)
                .ConfigureAwait(false);
        }
    }
}