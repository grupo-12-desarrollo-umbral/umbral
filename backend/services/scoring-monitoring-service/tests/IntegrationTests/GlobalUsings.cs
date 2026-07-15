global using FluentAssertions;
global using MediatR;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.DependencyInjection;
global using Moq;
global using umbral_backend.Application.Dtos.Rankings;
// The shared fixtures (PostgreSqlFixture, PostgreSqlCollection, DockerAvailability) live in the
// project's RootNamespace, but a number of test files still declare the older
// umbral_backend.Infrastructure.IntegrationTests.* namespace inherited from the scaffold. This
// global using makes the fixtures visible from both conventions.
global using umbral_backend.ScoringMonitoring.IntegrationTests;
global using Xunit;
