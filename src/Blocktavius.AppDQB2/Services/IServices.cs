using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2.Services;

interface IServices
{
	IStageLoader StageLoader();
}

sealed class DefaultServices : IServices
{
	private readonly IStageLoader stageLoader = new StageLoader();

	private DefaultServices() { }

	public static readonly DefaultServices Instance = new();

	public IStageLoader StageLoader() => stageLoader;
}
