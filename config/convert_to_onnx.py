# souschef_v19 checkpoint'ini Unity'siz ONNX'e çevirir.
# Kullanım: python convert_to_onnx.py
import numpy as np
from mlagents_envs.base_env import ActionSpec, BehaviorSpec, DimensionProperty, ObservationSpec, ObservationType
from mlagents.trainers.settings import TrainerSettings, NetworkSettings
from mlagents.trainers.policy.torch_policy import TorchPolicy
from mlagents.trainers.torch_entities.networks import SimpleActor
from mlagents.trainers.model_saver.torch_model_saver import TorchModelSaver
from mlagents.plugins.trainer_type import register_trainer_plugins

register_trainer_plugins()

RESULTS_DIR = r"D:\UnityGames\SmartKitchen\results\souschef_v19\SousChefBehavior"
BEHAVIOR_NAME = "SousChefBehavior"

# Behavior Parameters'taki değerlerle birebir aynı olmalı
obs_spec = ObservationSpec(
    shape=(13,),
    dimension_property=(DimensionProperty.NONE,),
    observation_type=ObservationType.DEFAULT,
    name="VectorSensor",
)
action_spec = ActionSpec(continuous_size=0, discrete_branches=(5, 2))
behavior_spec = BehaviorSpec(observation_specs=[obs_spec], action_spec=action_spec)

network_settings = NetworkSettings(normalize=True, hidden_units=128, num_layers=2)
trainer_settings = TrainerSettings(network_settings=network_settings)

policy = TorchPolicy(
    seed=0,
    behavior_spec=behavior_spec,
    network_settings=network_settings,
    actor_cls=SimpleActor,
    actor_kwargs={"conditional_sigma": False, "tanh_squash": False},
)

saver = TorchModelSaver(trainer_settings, RESULTS_DIR, load=True)
saver.register(policy)
saver.initialize_or_load(policy)  # RESULTS_DIR/checkpoint.pt dosyasını yükler

out_path = RESULTS_DIR + r"\SousChefBehavior"
saver.export(out_path, BEHAVIOR_NAME)
print("Export tamam:", out_path + ".onnx")
